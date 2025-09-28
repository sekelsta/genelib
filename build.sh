# To define the environment variable, put something like this in your .bashrc file:
# export VINTAGE_STORY_DEV="$HOME/software/vintagestory_dev"

# Intention: VINTAGE_STORY points to your Vintage Story install location
# while VINTAGE_STORY_DATA points to your Vintage Story data location
# Depending on how you set up those environment variables, you could use
# a different game install between playing and devving, or just a different
# data folder location (using the --dataPath argument on startup)
VS_DATA=${VINTAGE_STORY_DATA:-~/.config/VintagestoryData}

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
rm "${VS_DATA}"/Mods/genelib_*.zip
cp Build/Releases/genelib_*.zip "${VS_DATA}/Mods"

endtime=$(($(date +%s%N)/1000000))
buildtime=$(( endtime - prebuild ))
totaltime=$(( endtime - starttime ))
echo -e "${totaltime} milliseconds total: $((prebuild - starttime)) to validate and ${buildtime} to build"
