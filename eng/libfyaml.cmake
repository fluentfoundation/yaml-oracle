# Downloads libfyaml's release tarball, checks it against the pinned SHA-256 and builds the shared library into
# artifacts/native (fyaml.dll, libfyaml.so or libfyaml.dylib), where the worker loads it from.
#
#   cmake -P eng/libfyaml.cmake
#
# To try another release, change both LIBFYAML_VERSION and LIBFYAML_SHA256 (the digest GitHub shows for the asset).
cmake_minimum_required(VERSION 3.20)

set(LIBFYAML_VERSION "1.0.0-rc2")
set(LIBFYAML_SHA256 "8a88775eef05eaeea865f80b4a2fd5703bfa87ae46a3cc4654fe7095c5a1f6b3")
set(LIBFYAML_URL "https://github.com/pantoniou/libfyaml/releases/download/v${LIBFYAML_VERSION}/libfyaml-${LIBFYAML_VERSION}.tar.gz")

get_filename_component(ROOT "${CMAKE_CURRENT_LIST_DIR}/.." ABSOLUTE)
set(WORK "${ROOT}/artifacts/libfyaml/${LIBFYAML_VERSION}")
set(OUT "${ROOT}/artifacts/native")
set(TARBALL "${WORK}/libfyaml-${LIBFYAML_VERSION}.tar.gz")
set(SOURCE "${WORK}/libfyaml-${LIBFYAML_VERSION}")
set(BUILD "${WORK}/build")

if(EXISTS "${TARBALL}")
  file(SHA256 "${TARBALL}" actual)
  if(NOT actual STREQUAL LIBFYAML_SHA256)
    file(REMOVE "${TARBALL}")
  endif()
endif()
if(NOT EXISTS "${TARBALL}")
  message(STATUS "Downloading ${LIBFYAML_URL}")
  file(DOWNLOAD "${LIBFYAML_URL}" "${TARBALL}" TLS_VERIFY ON EXPECTED_HASH SHA256=${LIBFYAML_SHA256} STATUS status)
  list(GET status 0 code)
  if(NOT code EQUAL 0)
    message(FATAL_ERROR "Download failed: ${status}")
  endif()
endif()

if(NOT EXISTS "${SOURCE}/CMakeLists.txt")
  file(ARCHIVE_EXTRACT INPUT "${TARBALL}" DESTINATION "${WORK}")
endif()

execute_process(
  COMMAND "${CMAKE_COMMAND}" -S "${SOURCE}" -B "${BUILD}"
    -DCMAKE_BUILD_TYPE=Release -DBUILD_SHARED_LIBS=ON -DBUILD_TESTING=OFF -DENABLE_NETWORK=OFF
    -DENABLE_PYTHON_BINDINGS=OFF -DENABLE_PORTABLE_TARGET=ON
  COMMAND_ERROR_IS_FATAL ANY)
execute_process(
  COMMAND "${CMAKE_COMMAND}" --build "${BUILD}" --config Release --target fyaml --parallel
  COMMAND_ERROR_IS_FATAL ANY)

if(CMAKE_HOST_WIN32)
  file(GLOB_RECURSE built "${BUILD}/fyaml.dll")
  set(name "fyaml.dll")
elseif(CMAKE_HOST_APPLE)
  file(GLOB_RECURSE built "${BUILD}/libfyaml.dylib")
  set(name "libfyaml.dylib")
else()
  file(GLOB_RECURSE built "${BUILD}/libfyaml.so")
  set(name "libfyaml.so")
endif()
list(LENGTH built count)
if(NOT count EQUAL 1)
  message(FATAL_ERROR "Expected one ${name} under ${BUILD}, found: ${built}")
endif()
file(REAL_PATH "${built}" real) # libfyaml.so is a symlink to the versioned file
file(MAKE_DIRECTORY "${OUT}")
file(COPY_FILE "${real}" "${OUT}/${name}")
file(WRITE "${OUT}/libfyaml.version" "${LIBFYAML_VERSION}\n")
message(STATUS "libfyaml ${LIBFYAML_VERSION}: ${OUT}/${name}")
