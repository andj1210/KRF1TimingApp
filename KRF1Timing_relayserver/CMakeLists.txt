cmake_minimum_required(VERSION 3.10)
project(krf1_relayserver LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_executable(krf1_relayserver
    krf1_relayserver.cpp

    Platform.h
    TcpSocket.h
    TcpServer.h
    TcpServer.cpp
    DriverSession.h
    DriverSession.cpp

    F1DataDefs.h
    F1PacketExtractor.h
    F1PacketExtractor.cpp
)
find_package(Threads REQUIRED)
target_compile_definitions(krf1_relayserver PRIVATE NOMINMAX)

target_link_libraries(krf1_relayserver PRIVATE Threads::Threads)
if(WIN32)
    target_link_libraries(krf1_relayserver PRIVATE ws2_32)
endif()
