cmake_minimum_required(VERSION 3.20)
project(AmcTempoEngines C CXX)

# Wersja ABI jest w naglowku; tu tylko wersja pakietu do nazw plikow.
set(CMAKE_C_STANDARD 11)
set(CMAKE_C_STANDARD_REQUIRED ON)
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)
if(NOT CMAKE_BUILD_TYPE AND NOT CMAKE_CONFIGURATION_TYPES)
  set(CMAKE_BUILD_TYPE Release)
endif()

set(VENDOR "${CMAKE_CURRENT_SOURCE_DIR}/vendor")

# libsonic kompilowany jako "wnetrze": sonic2.h (Speedy) przedefiniowuje
# nazwy funkcji na sonicInt*, a wlasne, publiczne wersje dostarcza soniclib.c.
add_library(amc_sonic_internal OBJECT "${VENDOR}/sonic/sonic.c")
target_include_directories(amc_sonic_internal PRIVATE "${VENDOR}/sonic")
target_compile_definitions(amc_sonic_internal PRIVATE SONIC_INTERNAL=1)

add_library(amc_kissfft OBJECT "${VENDOR}/kissfft/kiss_fft.c")
target_include_directories(amc_kissfft PRIVATE "${VENDOR}/kissfft")

add_library(amc_speedy OBJECT
  "${VENDOR}/speedy/speedy.c"
  "${VENDOR}/speedy/soniclib.c")
target_include_directories(amc_speedy PRIVATE
  "${VENDOR}/speedy" "${VENDOR}/sonic" "${VENDOR}/kissfft")
# Speedy liczy widmo; KISS FFT jest wbudowane, bez zaleznosci od FFTW.
target_compile_definitions(amc_speedy PRIVATE KISS_FFT=1)

add_library(AmcTempoEngines SHARED
  "${CMAKE_CURRENT_SOURCE_DIR}/src/amc_tempo_engines.cpp"
  $<TARGET_OBJECTS:amc_sonic_internal>
  $<TARGET_OBJECTS:amc_speedy>
  $<TARGET_OBJECTS:amc_kissfft>)
target_include_directories(AmcTempoEngines PUBLIC
  "${CMAKE_CURRENT_SOURCE_DIR}/src"
  PRIVATE
  "${VENDOR}/speedy" "${VENDOR}/sonic" "${VENDOR}/kissfft"
  "${VENDOR}/signalsmith-stretch" "${VENDOR}/signalsmith-linear/include")
set_target_properties(AmcTempoEngines PROPERTIES PREFIX "" OUTPUT_NAME "AmcTempoEngines")
if(NOT MSVC)
  target_link_libraries(AmcTempoEngines PRIVATE m)
  target_compile_options(AmcTempoEngines PRIVATE -fvisibility=hidden)
endif()
if(MINGW)
  # Pelna statyka: DLL nie moze wymagac libstdc++/libgcc obok siebie.
  target_link_options(AmcTempoEngines PRIVATE
    -static -static-libgcc -static-libstdc++ -Wl,--no-undefined)
endif()

# Sonda PCM: mierzy rzeczywiste przetwarzanie, nie deklaracje.
add_executable(amc_tempo_probe "${CMAKE_CURRENT_SOURCE_DIR}/tests/amc_tempo_probe.cpp")
target_link_libraries(amc_tempo_probe PRIVATE AmcTempoEngines)
target_include_directories(amc_tempo_probe PRIVATE "${CMAKE_CURRENT_SOURCE_DIR}/src")
