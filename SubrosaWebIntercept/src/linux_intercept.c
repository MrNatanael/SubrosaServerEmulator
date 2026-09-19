#define _GNU_SOURCE
#include <dlfcn.h>
#include <fstream>
#include <string>
#include <filesystem>
#include <cstdio>
#include <arpa/inet.h>

static std::string g_ip = "127.0.0.1";
static unsigned short g_port = 80;

using connect_fn = int (*)(int, const sockaddr *, socklen_t);

extern "C" int connect(int sockfd, const sockaddr *addr, socklen_t addrlen)
{
    static connect_fn real_connect =
        (connect_fn)dlsym(RTLD_NEXT, "connect");

    if (addr && addr->sa_family == AF_INET)
    {
        sockaddr_in modified =
            *reinterpret_cast<const sockaddr_in *>(addr);

        char ip[INET_ADDRSTRLEN]{};
        inet_ntop(AF_INET, &modified.sin_addr,
                  ip, sizeof(ip));

        printf("[hook] connect(%s:%u)\n",
               ip, ntohs(modified.sin_port));

        if (ntohs(modified.sin_port) == 80)
        {
            printf("[hook] detected connection to http port, redirecting to %s:%u\n", g_ip.c_str(), g_port);
            modified.sin_port = htons(g_port);
            inet_pton(AF_INET, g_ip.c_str(),
                &modified.sin_addr);
        }
        
        return real_connect(
            sockfd,
            reinterpret_cast<const sockaddr *>(&modified),
            sizeof(modified));
    }

    return real_connect(sockfd, addr, addrlen);
}

__attribute__((constructor)) static void init()
{
    Dl_info info{};
    if (!dladdr(reinterpret_cast<void *>(&init), &info) || !info.dli_fname)
    {
        fprintf(stderr, "[hook] failed to locate hook.so\n");
        return;
    }
    std::filesystem::path hookPath(info.dli_fname);
    std::filesystem::path configPath = hookPath.parent_path() / "intercept.cfg";
    std::ifstream file(configPath);
    if (!file)
    {
        fprintf(stderr, "[hook] failed to open %s\n", configPath.c_str());
        return;
    }
    std::string ip;
    unsigned int port;
    if (file >> ip >> port && port <= 65535)
    {
        g_ip = ip;
        g_port = static_cast<unsigned short>(port);
        fprintf(stderr, "[hook] destination: %s:%u\n", g_ip.c_str(), g_port);
    }
    else
    {
        fprintf(stderr, "[hook] invalid intercept.cfg\n");
    }
}