/*
 * AMC - wspolny interfejs C dla nowych silnikow tempa.
 *
 * Wlasny, prosty adapter AMC. NIE jest to kopia zadnego cudzego wrappera:
 * wszystkie wywolania idza wprost do przypietych bibliotek upstream
 * (Speedy/Sonic, Signalsmith Stretch), ktorych licencje i hasze opisuje
 * ../PROVENANCE.md.
 *
 * Kontrakt wymiany probek: zawsze float w zakresie (-1, 1), uklad
 * przeplatany (interleaved), "ramka" (frame) to jedna probka ze WSZYSTKICH
 * kanalow. Liczby ramek, nie wartosci float, przechodza przez to API.
 */
#ifndef AMC_TEMPO_ENGINES_H_
#define AMC_TEMPO_ENGINES_H_

#include <stdint.h>

#if defined(_WIN32)
#define AMC_TEMPO_API __declspec(dllexport)
#else
#define AMC_TEMPO_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

/* Numery silnikow sa czescia ABI i odpowiadaja PlaybackTempoAlgorithm. */
typedef enum {
  AMC_TEMPO_ENGINE_SPEECH = 1, /* Google Speedy nad Sonic */
  AMC_TEMPO_ENGINE_MUSIC = 2   /* Signalsmith Stretch */
} AmcTempoEngineKind;

typedef enum {
  AMC_TEMPO_OK = 0,
  AMC_TEMPO_ERROR_ARGUMENT = 1,
  AMC_TEMPO_ERROR_UNSUPPORTED_ENGINE = 2,
  AMC_TEMPO_ERROR_OUT_OF_MEMORY = 3
} AmcTempoStatus;

typedef struct AmcTempoStream AmcTempoStream;

/* Wersja ABI. Zmiana ksztaltu funkcji podnosi ta liczbe; strona zarzadzana
 * odmawia pracy przy niezgodnosci, zamiast czytac pamiec po omacku. */
#define AMC_TEMPO_ABI_VERSION 1

AMC_TEMPO_API int32_t AmcTempoAbiVersion(void);

/*
 * Tworzy strumien. sampleRate w Hz, channels to 1 lub 2.
 * Zwraca NULL przy bledzie i ustawia *status.
 */
AMC_TEMPO_API AmcTempoStream* AmcTempoCreate(int32_t engine,
                                             int32_t sampleRate,
                                             int32_t channels,
                                             int32_t* status);

AMC_TEMPO_API void AmcTempoDestroy(AmcTempoStream* stream);

/* 1.0 = bez zmiany (tor obchodzony wyzej), 2.0 = dwa razy szybciej. */
AMC_TEMPO_API int32_t AmcTempoSetTempo(AmcTempoStream* stream, float tempo);

/* Mnoznik czestotliwosci probkowania: 1.0 = bez zmiany wysokosci. */
AMC_TEMPO_API int32_t AmcTempoSetPitch(AmcTempoStream* stream, float pitch);

/*
 * Sila nieliniowego przyspieszania Speedy: 0.0 = czysty Sonic (liniowo),
 * 1.0 = pelny Speedy. Dla silnika muzycznego zwraca
 * AMC_TEMPO_ERROR_UNSUPPORTED_ENGINE - nie udaje, ze ustawil.
 */
AMC_TEMPO_API int32_t AmcTempoSetNonlinearStrength(AmcTempoStream* stream,
                                                   float strength);

/* Liczba ramek wejscia faktycznie przyjetych (zwykle cala paczka). */
AMC_TEMPO_API int32_t AmcTempoWrite(AmcTempoStream* stream,
                                    const float* input,
                                    int32_t frameCount);

/* Liczba ramek wyjscia zapisanych do output; 0 oznacza "jeszcze nic". */
AMC_TEMPO_API int32_t AmcTempoRead(AmcTempoStream* stream,
                                   float* output,
                                   int32_t frameCapacity);

/* Konczy strumien: po tym AmcTempoRead oddaje ogon (drain). */
AMC_TEMPO_API int32_t AmcTempoFlush(AmcTempoStream* stream);

/* Czysci stan po przeskoku (seek). Ustawienia tempa/pitch zostaja. */
AMC_TEMPO_API int32_t AmcTempoReset(AmcTempoStream* stream);

/*
 * Ramki wejscia pochloniete przez silnik od ostatniego zerowania oraz ramki
 * wyjscia oddane. To JEDYNA rzetelna podstawa mapowania czasu dla silnika
 * NIELINIOWEGO: iloraz wyjscia i tempa nie odpowiada czasowi zrodla, bo
 * Speedy zmienia chwilowe tempo. Strona zarzadzana liczy pozycje z
 * ConsumedInputFrames, a nie z wyjscia razy tempo.
 */
AMC_TEMPO_API int64_t AmcTempoConsumedInputFrames(AmcTempoStream* stream);
AMC_TEMPO_API int64_t AmcTempoProducedOutputFrames(AmcTempoStream* stream);

/* Opoznienie silnika w ramkach wyjscia (Signalsmith ma je wbudowane). */
AMC_TEMPO_API int32_t AmcTempoOutputLatencyFrames(AmcTempoStream* stream);

#ifdef __cplusplus
}
#endif

#endif /* AMC_TEMPO_ENGINES_H_ */
