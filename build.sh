# To define the environment variable, put something like this in your .bashrc file:
# export VINTAGE_STORY_DEV="$HOME/software/vintagestory_dev"

starttime=$(($(date +%s%N)/1000000))

RED='\033[0;31m'
NC='\033[0m' # No Color

null_textured_shapes=$(grep -rl "#null" assets/)
# Only print anything if files were found
if [[ -n $null_textured_shapes ]]; then
    echo -e "${RED}These shape files contain null textures:"
    echo -e "${null_textured_shapes}${NC}"
fi

prebuild=$(($(date +%s%N)/1000000))

cp assets/genelib/lang/es-419.json assets/genelib/lang/es-es.json
dotnet run --project ./Build/CakeBuild/CakeBuild.csproj -- "$@"
rm assets/genelib/lang/es-es.json
rm -r bin/
rm -r src/obj/
rm "${VINTAGE_STORY_DEV}"/Mods/genelib_*.zip
cp Build/Releases/genelib_*.zip "${VINTAGE_STORY_DEV}/Mods"

endtime=$(($(date +%s%N)/1000000))
buildtime=$(( endtime - prebuild ))
totaltime=$(( endtime - starttime ))
echo -e "${totaltime} milliseconds total: $((prebuild - starttime)) to validate and ${buildtime} to build"
