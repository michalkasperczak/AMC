/*
 * Sonda PCM nowych silnikow tempa.
 *
 * Mierzy FAKTYCZNE przetwarzanie probek, a nie zadeklarowane. Material to
 * jawny fixture syntetyczny (ton z modulacja + przerwy ciszy, zeby Speedy mial
 * na czym pracowac nieliniowo) - to NIE jest odsluch i nie udaje odsluchu.
 *
 * Werdykt na stdout, kod wyjscia 0 tylko gdy wszystkie pomiary przeszly.
 */
#include "amc_tempo_engines.h"

#include <cmath>
#include <cstdio>
#include <cstring>
#include <string>
#include <vector>

namespace {

int g_failures = 0;

void Check(bool condition, const std::string& label) {
  std::printf("%s %s\n", condition ? "OK   " : "BLAD ", label.c_str());
  if (!condition) ++g_failures;
}

/* Fixture: ton 220 Hz z obwiednia i wtracona cisza; dla stereo prawy kanal
 * ma inna czestotliwosc, zeby bylo widac pomylenie kanalow. */
std::vector<float> MakeFixture(int sampleRate, int channels, double seconds) {
  const int frames = static_cast<int>(sampleRate * seconds);
  std::vector<float> pcm(static_cast<size_t>(frames) * channels, 0.0f);
  for (int frame = 0; frame < frames; ++frame) {
    const double t = static_cast<double>(frame) / sampleRate;
    const bool silence = std::fmod(t, 0.7) > 0.55;  /* pauzy jak w mowie */
    for (int c = 0; c < channels; ++c) {
      const double hz = (c == 0) ? 220.0 : 330.0;
      const double envelope = 0.5 + 0.5 * std::sin(2.0 * M_PI * 2.0 * t);
      pcm[static_cast<size_t>(frame) * channels + c] =
          silence ? 0.0f
                  : static_cast<float>(0.3 * envelope * std::sin(2.0 * M_PI * hz * t));
    }
  }
  return pcm;
}

struct RunResult {
  long long outputFrames = 0;
  long long consumedInput = 0;
  long long producedOutput = 0;
  bool allFinite = true;
  double peak = 0.0;
  double rightEnergy = 0.0;
  double leftEnergy = 0.0;
};

/* Przepuszcza caly fixture i zbiera dane o wyjsciu. */
RunResult Run(AmcTempoStream* stream,
              const std::vector<float>& pcm,
              int channels,
              std::vector<float>* collected = nullptr) {
  RunResult result;
  const int chunkFrames = 1024;
  const int readFrames = 4096;
  std::vector<float> out(static_cast<size_t>(readFrames) * channels);
  const int totalFrames = static_cast<int>(pcm.size() / channels);

  auto drain = [&]() {
    int got;
    while ((got = AmcTempoRead(stream, out.data(), readFrames)) > 0) {
      for (int frame = 0; frame < got; ++frame) {
        for (int c = 0; c < channels; ++c) {
          const float sample = out[static_cast<size_t>(frame) * channels + c];
          if (!std::isfinite(sample)) result.allFinite = false;
          const double magnitude = std::fabs(sample);
          if (magnitude > result.peak) result.peak = magnitude;
          if (c == 0) result.leftEnergy += sample * sample;
          else result.rightEnergy += sample * sample;
          if (collected != nullptr) collected->push_back(sample);
        }
      }
      result.outputFrames += got;
    }
  };

  for (int pos = 0; pos < totalFrames; pos += chunkFrames) {
    const int frames = std::min(chunkFrames, totalFrames - pos);
    const int accepted = AmcTempoWrite(
        stream, pcm.data() + static_cast<size_t>(pos) * channels, frames);
    if (accepted != frames) {
      std::printf("BLAD  zapis przyjal %d z %d ramek\n", accepted, frames);
      ++g_failures;
    }
    drain();
  }
  AmcTempoFlush(stream);
  drain();
  result.consumedInput = AmcTempoConsumedInputFrames(stream);
  result.producedOutput = AmcTempoProducedOutputFrames(stream);
  return result;
}

const char* EngineName(int engine) {
  return engine == AMC_TEMPO_ENGINE_SPEECH ? "mowa (Speedy)"
                                           : "muzyka (Signalsmith)";
}

/* Tempo faktycznie skraca material, wyjscie jest skonczone i nie cichnie. */
void TestTempoChangesLength(int engine, int channels) {
  const int sampleRate = 48000;
  const auto pcm = MakeFixture(sampleRate, channels, 4.0);
  const int inputFrames = static_cast<int>(pcm.size() / channels);

  long long baseline = 0;
  for (float tempo : {1.0f, 1.5f, 2.0f, 0.75f}) {
    int status = -1;
    AmcTempoStream* stream =
        AmcTempoCreate(engine, sampleRate, channels, &status);
    Check(stream != nullptr && status == AMC_TEMPO_OK,
          std::string("utworzenie strumienia ") + EngineName(engine) + ", " +
              std::to_string(channels) + " kan.");
    if (stream == nullptr) return;
    Check(AmcTempoSetTempo(stream, tempo) == AMC_TEMPO_OK,
          "ustawienie tempa " + std::to_string(tempo));

    const RunResult run = Run(stream, pcm, channels);
    const double ratio =
        static_cast<double>(inputFrames) / static_cast<double>(run.outputFrames);

    std::printf("      %s tempo %.2f: wejscie %d -> wyjscie %lld (iloraz %.3f)\n",
                EngineName(engine), tempo, inputFrames, run.outputFrames, ratio);

    Check(run.allFinite, "wszystkie probki skonczone (bez NaN/inf)");
    Check(run.peak > 0.01, "wyjscie nie jest cisza (szczyt " +
                               std::to_string(run.peak) + ")");
    Check(run.peak < 4.0, "wyjscie nie przesterowane");
    /* Tolerancja 12%: Speedy zmienia tempo chwilowo, wiec dlugosc nie wychodzi
     * dokladnie wejscie/tempo - i to jest poprawne zachowanie. */
    Check(std::fabs(ratio - tempo) / tempo < 0.12,
          "dlugosc wyjscia odpowiada tempu " + std::to_string(tempo));
    Check(run.consumedInput == inputFrames,
          "licznik pochlonietego wejscia zgadza sie z podanym materialem");
    Check(run.producedOutput == run.outputFrames,
          "licznik wyjscia zgadza sie z odczytanymi ramkami");

    if (channels == 2) {
      Check(run.leftEnergy > 0.0 && run.rightEnergy > 0.0,
            "oba kanaly niosa sygnal");
      /* Prawy kanal ma wyzszy ton, ale podobna moc; grube pomylenie kanalow
       * (np. zduplikowany lewy) dalo by iloraz daleko od 1. */
      const double balance = run.leftEnergy / run.rightEnergy;
      Check(balance > 0.3 && balance < 3.0,
            "moc kanalow zblizona, kanaly nie sa pomylone");
    }

    if (tempo == 1.0f) baseline = run.outputFrames;
    if (tempo == 2.0f && baseline > 0) {
      Check(run.outputFrames < baseline * 0.75,
            "tempo 2.0 daje wyraznie krotsze wyjscie niz 1.0");
    }
    AmcTempoDestroy(stream);
  }
}

/* Tempo 1.0 ma oddawac material bez zmiany dlugosci (neutralnosc). */
void TestNeutralTempo(int engine) {
  const int sampleRate = 48000;
  const int channels = 1;
  const auto pcm = MakeFixture(sampleRate, channels, 2.0);
  const int inputFrames = static_cast<int>(pcm.size());
  int status = -1;
  AmcTempoStream* stream = AmcTempoCreate(engine, sampleRate, channels, &status);
  if (stream == nullptr) return;
  AmcTempoSetTempo(stream, 1.0f);
  const RunResult run = Run(stream, pcm, channels);
  const double drift =
      std::fabs(static_cast<double>(run.outputFrames) - inputFrames) /
      inputFrames;
  std::printf("      %s tempo 1.0: wejscie %d -> wyjscie %lld (odchylka %.4f)\n",
              EngineName(engine), inputFrames, run.outputFrames, drift);
  Check(drift < 0.05, std::string("tempo 1.0 zachowuje dlugosc dla ") +
                          EngineName(engine));
  AmcTempoDestroy(stream);
}

/* Po przeskoku (reset) nie wycieka material ze starego miejsca. */
void TestResetDropsOldAudio(int engine) {
  const int sampleRate = 48000;
  const int channels = 1;
  const auto loud = MakeFixture(sampleRate, channels, 1.0);
  int status = -1;
  AmcTempoStream* stream = AmcTempoCreate(engine, sampleRate, channels, &status);
  if (stream == nullptr) return;
  AmcTempoSetTempo(stream, 1.5f);

  /* Napelniamy silnik, nie odczytujac - w buforach siedzi stary material. */
  AmcTempoWrite(stream, loud.data(), static_cast<int>(loud.size()));
  Check(AmcTempoReset(stream) == AMC_TEMPO_OK, "reset po przeskoku zwraca OK");
  Check(AmcTempoConsumedInputFrames(stream) == 0,
        "reset zeruje licznik wejscia");
  Check(AmcTempoProducedOutputFrames(stream) == 0,
        "reset zeruje licznik wyjscia");

  /* Teraz podajemy sama cisze: gdyby stary, glosny material przezyl reset,
   * zobaczylibysmy go na wyjsciu. */
  std::vector<float> silence(static_cast<size_t>(sampleRate), 0.0f);
  std::vector<float> out(4096);
  AmcTempoWrite(stream, silence.data(), static_cast<int>(silence.size()));
  AmcTempoFlush(stream);
  double peak = 0.0;
  int got;
  while ((got = AmcTempoRead(stream, out.data(), 4096)) > 0) {
    for (int i = 0; i < got; ++i) peak = std::max(peak, std::fabs((double)out[i]));
  }
  std::printf("      %s po resecie szczyt na ciszy: %.6f\n", EngineName(engine),
              peak);
  Check(peak < 0.01, std::string("reset nie przepuszcza starego dzwieku dla ") +
                         EngineName(engine));
  AmcTempoDestroy(stream);
}

/* Nieliniowosc Speedy jest FAKTYCZNIE wlaczona: wynik rozni sie od czystego
 * Sonica przy tym samym zadanym tempie. */
void TestSpeedyNonlinearityActive() {
  const int sampleRate = 48000;
  const int channels = 1;
  const auto pcm = MakeFixture(sampleRate, channels, 4.0);

  auto lengthFor = [&](float strength) -> long long {
    int status = -1;
    AmcTempoStream* stream =
        AmcTempoCreate(AMC_TEMPO_ENGINE_SPEECH, sampleRate, channels, &status);
    if (stream == nullptr) return -1;
    AmcTempoSetNonlinearStrength(stream, strength);
    AmcTempoSetTempo(stream, 2.0f);
    const RunResult run = Run(stream, pcm, channels);
    AmcTempoDestroy(stream);
    return run.outputFrames;
  };

  const long long linear = lengthFor(0.0f);     /* tylko Sonic */
  const long long nonlinear = lengthFor(1.0f);  /* pelny Speedy */
  std::printf("      Speedy tempo 2.0: liniowo %lld, nieliniowo %lld ramek\n",
              linear, nonlinear);
  Check(linear > 0 && nonlinear > 0, "oba przebiegi Speedy dalyby wyjscie");
  Check(linear != nonlinear,
        "nieliniowosc Speedy zmienia wynik wobec czystego Sonica");

  /* Signalsmith nie umie nieliniowosci i musi to powiedziec wprost. */
  int status = -1;
  AmcTempoStream* music =
      AmcTempoCreate(AMC_TEMPO_ENGINE_MUSIC, sampleRate, channels, &status);
  if (music != nullptr) {
    Check(AmcTempoSetNonlinearStrength(music, 1.0f) ==
              AMC_TEMPO_ERROR_UNSUPPORTED_ENGINE,
          "silnik muzyczny jawnie odmawia nieliniowosci, nie udaje");
    AmcTempoDestroy(music);
  }
}

/* Zly argument konczy sie bledem, a nie cichym "udalo sie". */
void TestArgumentGuards() {
  int status = -1;
  Check(AmcTempoCreate(99, 48000, 1, &status) == nullptr &&
            status == AMC_TEMPO_ERROR_UNSUPPORTED_ENGINE,
        "nieznany silnik: jawny blad");
  Check(AmcTempoCreate(AMC_TEMPO_ENGINE_SPEECH, 48000, 7, &status) == nullptr &&
            status == AMC_TEMPO_ERROR_ARGUMENT,
        "7 kanalow: jawny blad");
  Check(AmcTempoCreate(AMC_TEMPO_ENGINE_SPEECH, 10, 1, &status) == nullptr &&
            status == AMC_TEMPO_ERROR_ARGUMENT,
        "10 Hz: jawny blad");

  AmcTempoStream* stream =
      AmcTempoCreate(AMC_TEMPO_ENGINE_SPEECH, 48000, 1, &status);
  if (stream != nullptr) {
    Check(AmcTempoSetTempo(stream, 0.0f) == AMC_TEMPO_ERROR_ARGUMENT,
          "tempo 0: jawny blad");
    Check(AmcTempoSetTempo(stream, NAN) == AMC_TEMPO_ERROR_ARGUMENT,
          "tempo NaN: jawny blad");
    Check(AmcTempoSetPitch(stream, 1.0f) == AMC_TEMPO_OK,
          "pitch 1.0 przyjety");
    AmcTempoDestroy(stream);
  }
  Check(AmcTempoSetTempo(nullptr, 1.5f) == AMC_TEMPO_ERROR_ARGUMENT,
        "puste wskazanie strumienia: jawny blad");
  Check(AmcTempoAbiVersion() == AMC_TEMPO_ABI_VERSION,
        "wersja ABI zgodna z naglowkiem");
}

}  // namespace

int main() {
  std::printf("== sonda PCM nowych silnikow tempa AMC ==\n");
  TestArgumentGuards();
  for (int engine : {AMC_TEMPO_ENGINE_SPEECH, AMC_TEMPO_ENGINE_MUSIC}) {
    for (int channels : {1, 2}) {
      TestTempoChangesLength(engine, channels);
    }
    TestNeutralTempo(engine);
    TestResetDropsOldAudio(engine);
  }
  TestSpeedyNonlinearityActive();

  if (g_failures == 0) {
    std::printf("\nWSZYSTKIE POMIARY PRZESZLY\n");
    return 0;
  }
  std::printf("\nNIEZGODNOSCI: %d\n", g_failures);
  return 1;
}
