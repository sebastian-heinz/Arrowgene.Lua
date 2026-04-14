#!/usr/bin/env bash
#
# Compiles luac binaries for all supported platforms.
#
# Output:
#   tools/luac/linux-x64/luac50..luac55
#   tools/luac/linux-arm64/luac50..luac55
#   tools/luac/osx-arm64/luac50..luac55
#   tools/luac/windows-x64/luac50.exe..luac55.exe
#
# Requirements:
#   - Docker (for Linux and Windows cross-compilation)
#   - Xcode Command Line Tools (for macOS builds)
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
LUAC_DIR="${SCRIPT_DIR}/luac"

LUA_VERSIONS=(
    "5.0.3"
    "5.1.5"
    "5.2.4"
    "5.3.6"
    "5.4.7"
    "5.5.0"
)

build_linux() {
    local PLATFORM="$1"   # e.g. linux/amd64
    local OUT_DIR="$2"    # e.g. .../linux-x64
    mkdir -p "${OUT_DIR}"

    echo ""
    echo "=============================="
    echo "  Platform: ${PLATFORM}"
    echo "=============================="

    for FULL_VER in "${LUA_VERSIONS[@]}"; do
        local MAJOR_MINOR="${FULL_VER%.*}"
        local SHORT="${MAJOR_MINOR//./}"
        local BINARY_NAME="luac${SHORT}"

        echo "--- ${BINARY_NAME} (Lua ${FULL_VER}) ---"

        # Lua 5.0 uses unsigned long for Instruction. On LP64 targets
        # (linux-x64, linux-arm64, macOS), that becomes 8 bytes, so patch it
        # to unsigned int before building.

        docker run --rm \
            --platform "${PLATFORM}" \
            -v "${OUT_DIR}:/out" \
            debian:bookworm-slim \
            bash -c "
                set -e
                apt-get update -qq
                apt-get install -y -qq --no-install-recommends \
                    build-essential libreadline-dev curl ca-certificates >/dev/null 2>&1

                curl -fsSL 'https://www.lua.org/ftp/lua-${FULL_VER}.tar.gz' | tar xz
                cd 'lua-${FULL_VER}'

                if [ '${MAJOR_MINOR}' = '5.0' ]; then
                    sed -i 's/typedef unsigned long Instruction;/typedef unsigned int Instruction;/' src/llimits.h
                    make 2>&1
                    cp bin/luac '/out/${BINARY_NAME}'
                else
                    make linux 2>&1
                    cp src/luac '/out/${BINARY_NAME}'
                fi

                chmod +x '/out/${BINARY_NAME}'
                echo '  ${BINARY_NAME} ok'
            "
    done
}

build_windows() {
    local OUT_DIR="${LUAC_DIR}/windows-x64"
    mkdir -p "${OUT_DIR}"

    local CC="x86_64-w64-mingw32-gcc"
    local AR="x86_64-w64-mingw32-ar rcu"
    local RANLIB="x86_64-w64-mingw32-ranlib"
    local STRIP="x86_64-w64-mingw32-strip"

    echo ""
    echo "=============================="
    echo "  Platform: windows-x64 (mingw cross-compile)"
    echo "=============================="

    for FULL_VER in "${LUA_VERSIONS[@]}"; do
        local MAJOR_MINOR="${FULL_VER%.*}"
        local SHORT="${MAJOR_MINOR//./}"
        local BINARY_NAME="luac${SHORT}.exe"

        echo "--- ${BINARY_NAME} (Lua ${FULL_VER}) ---"

        # Windows x64 uses LLP64, so unsigned long remains 32-bit for Lua 5.0
        # and does not need the Instruction-width patch used on LP64 targets.

        docker run --rm \
            --platform "linux/amd64" \
            -v "${OUT_DIR}:/out" \
            debian:bookworm-slim \
            bash -c "
                set -e
                apt-get update -qq
                apt-get install -y -qq --no-install-recommends \
                    gcc-mingw-w64-x86-64 curl ca-certificates make >/dev/null 2>&1

                curl -fsSL 'https://www.lua.org/ftp/lua-${FULL_VER}.tar.gz' | tar xz
                cd 'lua-${FULL_VER}'

                if [ '${MAJOR_MINOR}' = '5.0' ]; then
                    sed -i 's|^CC=.*|CC= ${CC}|'             config
                    sed -i 's|^AR=.*|AR= ${AR}|'             config
                    sed -i 's|^RANLIB=.*|RANLIB= ${RANLIB}|' config
                    sed -i 's|^EXTRA_LIBS=.*|EXTRA_LIBS=|'    config
                    sed -i 's|^LOADLIB=.*|LOADLIB=|'          config
                    sed -i 's|^DLLIB=.*|DLLIB=|'              config
                    make 2>&1
                    cp bin/luac.exe '/out/${BINARY_NAME}'
                else
                    # Use generic target — we only need the static luac binary
                    make generic CC='${CC}' AR='${AR}' RANLIB='${RANLIB}' 2>&1
                    cp src/luac.exe '/out/${BINARY_NAME}'
                fi

                ${STRIP} '/out/${BINARY_NAME}' 2>/dev/null || true
                echo '  ${BINARY_NAME} ok'
            "
    done
}

build_native_macos() {
    local OUT_DIR="${LUAC_DIR}/osx-arm64"
    mkdir -p "${OUT_DIR}"
    local TMP_DIR
    TMP_DIR="$(mktemp -d)"

    echo ""
    echo "=============================="
    echo "  Platform: osx-arm64 (native)"
    echo "=============================="

    for FULL_VER in "${LUA_VERSIONS[@]}"; do
        local MAJOR_MINOR="${FULL_VER%.*}"
        local SHORT="${MAJOR_MINOR//./}"
        local BINARY_NAME="luac${SHORT}"

        echo "--- ${BINARY_NAME} (Lua ${FULL_VER}) ---"

        curl -fsSL "https://www.lua.org/ftp/lua-${FULL_VER}.tar.gz" | tar xz -C "${TMP_DIR}"

        pushd "${TMP_DIR}/lua-${FULL_VER}" > /dev/null

        if [ "${MAJOR_MINOR}" = "5.0" ]; then
            # Lua 5.0 defines Instruction as "unsigned long" which is 8 bytes on
            # arm64, producing bytecode the decompiler cannot handle (it expects
            # 4-byte instructions, matching upstream unluac).  Patch to uint32.
            sed -i '' 's/typedef unsigned long Instruction;/typedef unsigned int Instruction;/' src/llimits.h
            make 2>&1
            cp bin/luac "${OUT_DIR}/${BINARY_NAME}"
        else
            make macosx 2>&1
            cp src/luac "${OUT_DIR}/${BINARY_NAME}"
        fi

        chmod +x "${OUT_DIR}/${BINARY_NAME}"
        echo "  ${BINARY_NAME} ok"
        popd > /dev/null
    done

    rm -rf "${TMP_DIR}"
}

# --- Linux builds via Docker ---
build_linux "linux/amd64"  "${LUAC_DIR}/linux-x64"
build_linux "linux/arm64"  "${LUAC_DIR}/linux-arm64"

# --- Windows build via Docker + mingw ---
build_windows

# --- macOS build (native) ---
build_native_macos

echo ""
echo "=== All binaries ==="
find "${LUAC_DIR}" -name 'luac*' -type f | sort | while read -r f; do
    printf "  %-45s %s\n" "${f#"${LUAC_DIR}/"}" "$(file -b "${f}" | cut -d, -f1-2)"
done
