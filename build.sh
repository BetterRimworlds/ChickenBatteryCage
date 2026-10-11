#!/bin/bash
# ==== build.sh ====
#
# ./build.sh [1] [fast]
#   1     build once. The file watcher below is disabled either way.
#   fast  define CBC_FAST_DECOMPOSITION. Fertilizer stacks then dissolve in one
#         day, or four times as fast while unroofed in bad weather. A build
#         without this argument keeps the ten-day food-rot timer.
#
# Check if inotifywait is installed
if [ -z "$(which inotifywait)" ]; then
    echo "inotifywait not installed."
    echo "Install the inotify-tools package and try again."
    exit 1
fi

# Directory to monitor for changes
dir="./Source"
MOD=$(basename $PWD)
if [[ ! -f "Source/${MOD}.sln" ]]; then
    MOD=$(basename "$(ls Source/*.sln | head -n1)" .sln)
fi

# Define the path to your solution file
solutionPath="Source/${MOD}.sln"

# Define an array of configurations
configurations=("v1.6")

fast_decomposition=false
one_shot=false
for arg in "$@"; do
    case "$arg" in
        fast) fast_decomposition=true ;;
        1) one_shot=true ;;
        *)
            echo "Unknown argument: $arg" >&2
            echo "Usage: ./build.sh [1] [fast]" >&2
            exit 1
            ;;
    esac
done

build_props=()
if [[ "$fast_decomposition" == true ]]; then
    build_props+=(-p:FastDecomposition=true)
    echo "Fast fertilizer decomposition enabled: a stack dissolves in 1 day, or faster while unroofed in bad weather."
fi

dotnet restore "$solutionPath"

function sync_mod() {
    if [ -d "${MOD}/Languages/Spanish" ]; then
        rm -rf "${MOD}/Languages/SpanishLatin"
        cp -avf ${MOD}/Languages/Spanish ${MOD}/Languages/SpanishLatin
    fi

    # Copy over the mod directory.
    rsync -a ${MOD} /rimworld/1.6/Mods/

    # Copy over and reformat the README.
    cp README.md /rimworld/1.6/Mods/${MOD}
    unix2dos /rimworld/1.6/Mods/${MOD}/README.md

    rm -rf /rimworld/1.6-steam/Mods/${MOD}

    cp -af /rimworld/1.6/Mods/${MOD} /rimworld/1.6-steam/Mods
}

function build() {
    rm -rf /rimworld/1.6/Mods/${MOD}

    # Loop through each configuration and build it
    local pids=()
    for config in "${configurations[@]}"; do
        echo "Building for configuration: $config"
        dotnet build --no-restore "$solutionPath" --configuration "Release $config" "${build_props[@]}" &
        pids+=($!)
    done

    local failed=0
    for pid in "${pids[@]}"; do
        wait "$pid" || { echo "Build failed (PID $pid)"; failed=1; }
    done

    if [[ $failed -eq 1 ]]; then
        echo "One or more builds failed. Aborting sync."
        return 1
    fi

    # Run the unit tests when a test project exists; failures abort the sync.
    local testProject="Tests/${MOD}.Tests.csproj"
    if [[ -f "$testProject" ]]; then
        echo "Running unit tests"
        dotnet test "$testProject" --nologo || { echo "Unit tests failed. Aborting sync."; return 1; }
    fi

    sync_mod
    echo "All builds completed!"
}

build || exit 1

if [[ "$one_shot" == true || "$1" == "1" ]]; then
    echo "Done"
    exit
fi

## Watch for changes to .cs and XML files in the directory and subdirectories
#inotifywait --recursive --monitor --format "%e %w%f" \
#    --exclude '/\.idea($|/)' \
#    --event modify,move,create,delete "$dir" "$MOD" |
#    while read event fullpath; do
#        if [[ "$fullpath" == "$dir"* && "$fullpath" == *.cs ]]; then
#            echo "Running build for $fullpath"
#            build || echo "Build failed, skipping sync."
#        elif [[ "$fullpath" == "$MOD"* && "$fullpath" == *.xml ]]; then
#            echo "Running sync_mod for $fullpath"
#            sync_mod
#        fi
#    done
