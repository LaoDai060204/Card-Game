include "lzma/."
include "spmemvfs/."

project "ygoserver"
    kind "SharedLib"

    files { "gframe.cpp", "gframe.h",
            "config.h", "serverapi.cpp", "serverapi.h",
            "game.cpp", "game.h", "myfilesystem.h",
            "deck_manager.cpp", "deck_manager.h",
            "data_manager.cpp", "data_manager.h",
            "replay.cpp", "replay.h",
            "netserver.cpp", "netserver.h",
            "single_duel.cpp", "single_duel.h",
            "tag_duel.cpp", "tag_duel.h" }
    includedirs { "spmemvfs", "../ocgcore", "../event/include", "../event/windows", "../sqlite3", "../Irrlicht/source/Irrlicht", "../Irrlicht/include" }
    links { "ocgcore", "clzma", "sqlite3", "lua" , "event", "irrlicht", "cspmemvfs", "ws2_32"}
    defines { "YGOPRO_SERVER_MODE", "SERVER_ZIP_SUPPORT", "SERVER_PRO2_SUPPORT", "SERVER_PRO3_SUPPORT", "_IRR_WCHAR_FILESYSTEM", "SERVER_TAG_SURRENDER_CONFIRM" }
