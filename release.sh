#!/bin/bash
DOTNET_VERSION='10.0'
RELEASE_SCRIPT_URL="https://raw.githubusercontent.com/hmlendea/dotnet-release-scripts/refs/heads/master/dotnet/${DOTNET_VERSION}.sh"
wget --quiet -O - "${RELEASE_SCRIPT_URL}" | bash /dev/stdin ${@}
