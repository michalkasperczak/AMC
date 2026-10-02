/*
 * AMC - wlasny adapter nowych silnikow tempa pod wspolne API C.
 *
 * Mowa  (AMC_TEMPO_ENGINE_SPEECH): Google Speedy nad libsonic. Model PUSH:
 *       oddajemy tyle, ile silnik sam wyprodukowal.
 * Muzyka (AMC_TEMPO_ENGINE_MUSIC): Signalsmith Stretch. Model PULL: silnik
 *       chce z gory wiedziec, ile ramek wejscia i wyjscia ma przerobic, wiec
 *       trzymamy wlasna kolejke wejscia przed nim.
 *
 * Granice mapowania czasu: Speedy zmienia tempo CHWILOWE, wiec czas zrodla NIE
 * jest wyjsciem razy tempo. Dlatego adapter zlicza faktycznie POCHLONIETE
 * ramki wejscia i podaje je na zewnatrz (AmcTempoConsumedInputFrames) - to
 * jedyna rzetelna, a przy tym tania podstawa mapowania pozycji. Zadne
 * zgadywanie tempa chwilowego nie jest tu potrzebne.
 */
#include "amc_tempo_engines.h"

#include <algorithm>
#include <cmath>
#include <cstring>
#include <memory>
#include <new>
#include <vector>

extern "C" {
#include "sonic2.h"
}

#include "signalsmith-stretch.h"

namespace {

constexpr int kMaxChannels = 2;

bool IsFiniteAndPositive(float value) {
  return std::isfinite(value) && value > 0.0f;
}

class Engine {
 public:
  virtual ~Engine() = default;
  virtual bool SetTempo(float tempo) = 0;
  virtual bool SetPitch(float pitch) = 0;
  virtual bool SetNonlinearStrength(float strength) = 0;
  virtual int Write(const float* input, int frameCount) = 0;
  virtual int Read(float* output, int frameCapacity) = 0;
  virtual void Flush() = 0;
  virtual void Reset() = 0;
  virtual int OutputLatencyFrames() const = 0;

  int64_t consumedInput = 0;
  int64_t producedOutput = 0;
};

/* ------------------------------ Mowa: Speedy ------------------------------ */

class SpeedyEngine final : public Engine {
 public:
  SpeedyEngine(int sampleRate, int channels)
      : channels_(channels),
        stream_(sonicCreateStream(sampleRate, channels)) {
    if (stream_ != nullptr) {
      /* Pelny Speedy. Zero znaczyloby "tylko Sonic", czyli nie to, co
       * uzytkownik wybral jako tryb mowy. */
      sonicEnableNonlinearSpeedup(stream_, 1.0f);
      /* 0.1 to wartosc zalecana w naglowku upstream: przy nadmiarze czasu
       * delikatnie dociaga sredni czas do zadanego tempa. */
      sonicSetDurationFeedbackStrength(stream_, 0.1f);
    }
  }

  ~SpeedyEngine() override {
    if (stream_ != nullptr) sonicDestroyStream(stream_);
  }

  bool valid() const { return stream_ != nullptr; }

  bool SetTempo(float tempo) override {
    sonicSetSpeed(stream_, tempo);
    tempo_ = tempo;
    return true;
  }

  bool SetPitch(float pitch) override {
    /* libsonic liczy wysokosc przez zmiane czestotliwosci probkowania. */
    sonicSetRate(stream_, pitch);
    pitch_ = pitch;
    return true;
  }

  bool SetNonlinearStrength(float strength) override {
    /* Przypiety upstream ma JEDEN parametr tuningu (plik soniclib.c), mimo
     * komentarza w naglowku o czasie normalizacji. Nie dokladamy tu drugiego
     * argumentu, zeby nie wymyslac API. */
    sonicEnableNonlinearSpeedup(stream_, strength);
    return true;
  }

  int Write(const float* input, int frameCount) override {
    /* UWAGA na kontrakt upstreamu: sonicWriteFloatToStream zwraca FLAGE
     * powodzenia (1/0), a nie liczbe przyjetych ramek - w petli przepisuje
     * caly podany blok (patrz vendor/speedy/soniclib.c, "return 1" na koncu
     * petli while). Dlatego przy powodzeniu liczymy caly frameCount, a nie
     * wartosc zwrotna. Pomylka w te strone dawalaby "przyjeto 1 ramke". */
    const int ok = sonicWriteFloatToStream(stream_, input, frameCount);
    if (ok <= 0) return -1;
    consumedInput += frameCount;
    return frameCount;
  }

  int Read(float* output, int frameCapacity) override {
    const int frames = sonicReadFloatFromStream(stream_, output, frameCapacity);
    if (frames > 0) producedOutput += frames;
    return frames;
  }

  void Flush() override { sonicFlushStream(stream_); }

  void Reset() override {
    /* libsonic nie ma osobnego "reset", a flush zostawia ogon do odczytu.
     * Zeby po przeskoku NIC ze starego miejsca nie wyciekalo, oproznamy
     * strumien do konca i wyrzucamy te probki. */
    sonicFlushStream(stream_);
    std::vector<float> sink(static_cast<size_t>(4096) * channels_);
    while (sonicReadFloatFromStream(stream_, sink.data(), 4096) > 0) {
    }
    consumedInput = 0;
    producedOutput = 0;
    /* Flush nie gubi ustawien, ale przywracamy je jawnie - tak zachowanie nie
     * zalezy od szczegolu implementacji upstream. */
    sonicSetSpeed(stream_, tempo_);
    sonicSetRate(stream_, pitch_);
  }

  int OutputLatencyFrames() const override { return 0; }

 private:
  int channels_;
  sonicStream stream_;
  float tempo_ = 1.0f;
  float pitch_ = 1.0f;
};

/* --------------------------- Muzyka: Signalsmith -------------------------- */

class StretchEngine final : public Engine {
 public:
  StretchEngine(int sampleRate, int channels) : channels_(channels) {
    stretch_.presetDefault(channels, static_cast<float>(sampleRate));
    pending_.resize(static_cast<size_t>(channels));
    scratchIn_.resize(static_cast<size_t>(channels));
    scratchOut_.resize(static_cast<size_t>(channels));
  }

  bool SetTempo(float tempo) override {
    tempo_ = tempo;
    return true;
  }

  bool SetPitch(float pitch) override {
    stretch_.setTransposeFactor(pitch);
    pitch_ = pitch;
    return true;
  }

  bool SetNonlinearStrength(float) override {
    /* Signalsmith nie ma nieliniowego przyspieszania. Jawne "nie umiem" jest
     * lepsze niz ciche przyjecie wartosci, ktora nic nie robi. */
    return false;
  }

  int Write(const float* input, int frameCount) override {
    for (int frame = 0; frame < frameCount; ++frame) {
      for (int c = 0; c < channels_; ++c) {
        pending_[static_cast<size_t>(c)].push_back(
            input[static_cast<size_t>(frame) * channels_ + c]);
      }
    }
    consumedInput += frameCount;
    return frameCount;
  }

  int Read(float* output, int frameCapacity) override {
    const int available = static_cast<int>(pending_[0].size());
    /* Ile ramek wejscia zuzyje blok wyjscia tej dlugosci. */
    int outputFrames = frameCapacity;
    int inputFrames = static_cast<int>(std::lround(outputFrames * tempo_));
    if (inputFrames > available) {
      if (!flushed_) {
        /* Jeszcze nie ma z czego policzyc pelnej paczki: skrocamy ja do tego,
         * co pokrywa zgromadzone wejscie. */
        outputFrames = static_cast<int>(std::floor(available / tempo_));
        if (outputFrames <= 0) return 0;
        inputFrames = static_cast<int>(std::lround(outputFrames * tempo_));
        if (inputFrames > available) inputFrames = available;
      } else {
        inputFrames = available;
        outputFrames = static_cast<int>(std::floor(available / tempo_));
      }
    }

    if (outputFrames <= 0) {
      if (!flushed_ || drained_) return 0;
      /* Ogon: wejscie sie skonczylo, ale w silniku zostal material. */
      return ReadTail(output, frameCapacity);
    }

    PreparePointers(inputFrames, outputFrames);
    stretch_.process(scratchIn_.data(), inputFrames, scratchOut_.data(),
                     outputFrames);
    for (int c = 0; c < channels_; ++c) {
      auto& channel = pending_[static_cast<size_t>(c)];
      channel.erase(channel.begin(), channel.begin() + inputFrames);
    }
    Interleave(output, outputFrames);
    producedOutput += outputFrames;
    return outputFrames;
  }

  void Flush() override { flushed_ = true; }

  void Reset() override {
    stretch_.reset();
    for (auto& channel : pending_) channel.clear();
    flushed_ = false;
    drained_ = false;
    consumedInput = 0;
    producedOutput = 0;
    stretch_.setTransposeFactor(pitch_);
  }

  int OutputLatencyFrames() const override { return stretch_.outputLatency(); }

 private:
  int ReadTail(float* output, int frameCapacity) {
    const int tail = std::min(frameCapacity, stretch_.outputLatency());
    if (tail <= 0) {
      drained_ = true;
      return 0;
    }
    for (int c = 0; c < channels_; ++c) {
      outBuffers_[static_cast<size_t>(c)].assign(static_cast<size_t>(tail),
                                                 0.0f);
      scratchOut_[static_cast<size_t>(c)] =
          outBuffers_[static_cast<size_t>(c)].data();
    }
    stretch_.flush(scratchOut_.data(), tail);
    Interleave(output, tail);
    drained_ = true;
    producedOutput += tail;
    return tail;
  }

  void PreparePointers(int inputFrames, int outputFrames) {
    outBuffers_.resize(static_cast<size_t>(channels_));
    for (int c = 0; c < channels_; ++c) {
      scratchIn_[static_cast<size_t>(c)] =
          pending_[static_cast<size_t>(c)].data();
      outBuffers_[static_cast<size_t>(c)].assign(
          static_cast<size_t>(outputFrames), 0.0f);
      scratchOut_[static_cast<size_t>(c)] =
          outBuffers_[static_cast<size_t>(c)].data();
    }
    (void)inputFrames;
  }

  void Interleave(float* output, int frames) {
    for (int frame = 0; frame < frames; ++frame) {
      for (int c = 0; c < channels_; ++c) {
        output[static_cast<size_t>(frame) * channels_ + c] =
            outBuffers_[static_cast<size_t>(c)][static_cast<size_t>(frame)];
      }
    }
  }

  int channels_;
  signalsmith::stretch::SignalsmithStretch<float> stretch_;
  std::vector<std::vector<float>> pending_;
  std::vector<std::vector<float>> outBuffers_;
  std::vector<float*> scratchIn_;
  std::vector<float*> scratchOut_;
  float tempo_ = 1.0f;
  float pitch_ = 1.0f;
  bool flushed_ = false;
  bool drained_ = false;
};

}  // namespace

struct AmcTempoStream {
  std::unique_ptr<Engine> engine;
  int channels = 0;
};

extern "C" {

int32_t AmcTempoAbiVersion(void) { return AMC_TEMPO_ABI_VERSION; }

AmcTempoStream* AmcTempoCreate(int32_t engine,
                               int32_t sampleRate,
                               int32_t channels,
                               int32_t* status) {
  auto fail = [status](AmcTempoStatus code) -> AmcTempoStream* {
    if (status != nullptr) *status = static_cast<int32_t>(code);
    return nullptr;
  };

  if (sampleRate < 4000 || sampleRate > 384000) {
    return fail(AMC_TEMPO_ERROR_ARGUMENT);
  }
  if (channels < 1 || channels > kMaxChannels) {
    return fail(AMC_TEMPO_ERROR_ARGUMENT);
  }

  std::unique_ptr<Engine> built;
  if (engine == AMC_TEMPO_ENGINE_SPEECH) {
    auto speech = std::make_unique<SpeedyEngine>(sampleRate, channels);
    if (!speech->valid()) return fail(AMC_TEMPO_ERROR_OUT_OF_MEMORY);
    built = std::move(speech);
  } else if (engine == AMC_TEMPO_ENGINE_MUSIC) {
    built = std::make_unique<StretchEngine>(sampleRate, channels);
  } else {
    return fail(AMC_TEMPO_ERROR_UNSUPPORTED_ENGINE);
  }

  auto* stream = new (std::nothrow) AmcTempoStream();
  if (stream == nullptr) return fail(AMC_TEMPO_ERROR_OUT_OF_MEMORY);
  stream->engine = std::move(built);
  stream->channels = channels;
  if (status != nullptr) *status = AMC_TEMPO_OK;
  return stream;
}

void AmcTempoDestroy(AmcTempoStream* stream) { delete stream; }

int32_t AmcTempoSetTempo(AmcTempoStream* stream, float tempo) {
  if (stream == nullptr || !IsFiniteAndPositive(tempo) || tempo < 0.25f ||
      tempo > 4.0f) {
    return AMC_TEMPO_ERROR_ARGUMENT;
  }
  return stream->engine->SetTempo(tempo) ? AMC_TEMPO_OK
                                         : AMC_TEMPO_ERROR_ARGUMENT;
}

int32_t AmcTempoSetPitch(AmcTempoStream* stream, float pitch) {
  if (stream == nullptr || !IsFiniteAndPositive(pitch) || pitch < 0.25f ||
      pitch > 4.0f) {
    return AMC_TEMPO_ERROR_ARGUMENT;
  }
  return stream->engine->SetPitch(pitch) ? AMC_TEMPO_OK
                                         : AMC_TEMPO_ERROR_ARGUMENT;
}

int32_t AmcTempoSetNonlinearStrength(AmcTempoStream* stream, float strength) {
  if (stream == nullptr || !std::isfinite(strength) || strength < 0.0f ||
      strength > 1.0f) {
    return AMC_TEMPO_ERROR_ARGUMENT;
  }
  return stream->engine->SetNonlinearStrength(strength)
             ? AMC_TEMPO_OK
             : AMC_TEMPO_ERROR_UNSUPPORTED_ENGINE;
}

int32_t AmcTempoWrite(AmcTempoStream* stream,
                      const float* input,
                      int32_t frameCount) {
  if (stream == nullptr || input == nullptr || frameCount < 0) {
    return -AMC_TEMPO_ERROR_ARGUMENT;
  }
  if (frameCount == 0) return 0;
  return stream->engine->Write(input, frameCount);
}

int32_t AmcTempoRead(AmcTempoStream* stream,
                     float* output,
                     int32_t frameCapacity) {
  if (stream == nullptr || output == nullptr || frameCapacity < 0) {
    return -AMC_TEMPO_ERROR_ARGUMENT;
  }
  if (frameCapacity == 0) return 0;
  return stream->engine->Read(output, frameCapacity);
}

int32_t AmcTempoFlush(AmcTempoStream* stream) {
  if (stream == nullptr) return AMC_TEMPO_ERROR_ARGUMENT;
  stream->engine->Flush();
  return AMC_TEMPO_OK;
}

int32_t AmcTempoReset(AmcTempoStream* stream) {
  if (stream == nullptr) return AMC_TEMPO_ERROR_ARGUMENT;
  stream->engine->Reset();
  return AMC_TEMPO_OK;
}

int64_t AmcTempoConsumedInputFrames(AmcTempoStream* stream) {
  return stream == nullptr ? -1 : stream->engine->consumedInput;
}

int64_t AmcTempoProducedOutputFrames(AmcTempoStream* stream) {
  return stream == nullptr ? -1 : stream->engine->producedOutput;
}

int32_t AmcTempoOutputLatencyFrames(AmcTempoStream* stream) {
  return stream == nullptr ? -1 : stream->engine->OutputLatencyFrames();
}

}  // extern "C"
