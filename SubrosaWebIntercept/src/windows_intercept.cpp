#include <windows.h>
#include <winsock.h>

#include <fstream>
#include <string>
#include <filesystem>
#include <cstdio>

static std::string g_ip = "127.0.0.1";
static unsigned short g_port = 80;

using connect_fn = int (*)(SOCKET, const sockaddr *, int);

static connect_fn real_connect = nullptr;
static HMODULE real_wsock32 = nullptr;

static void load_config()
{
    char path[MAX_PATH];

    HMODULE module = nullptr;

    if (!GetModuleHandleExA(
            GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS |
                GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCSTR>(&load_config),
            &module))
    {
        fprintf(stderr, "[hook] failed to locate DLL\n");
        return;
    }

    DWORD length = GetModuleFileNameA(
        module,
        path,
        sizeof(path));

    if (length == 0 || length >= sizeof(path))
    {
        fprintf(stderr, "[hook] failed to get DLL path\n");
        return;
    }

    std::filesystem::path dllPath(path);
    std::filesystem::path configPath =
        dllPath.parent_path() / "intercept.cfg";

    std::ifstream file(configPath);

    if (!file)
    {
        fprintf(stderr, "[hook] failed to open %s\n",
                configPath.string().c_str());
        return;
    }

    std::string ip;
    unsigned int port;

    if (file >> ip >> port && port <= 65535)
    {
        g_ip = ip;
        g_port = static_cast<unsigned short>(port);

        fprintf(stderr,
                "[hook] destination: %s:%u\n",
                g_ip.c_str(),
                g_port);
    }
    else
    {
        fprintf(stderr, "[hook] invalid intercept.cfg\n");
    }
}

static void resolve_connect()
{
    char systemPath[MAX_PATH];

    UINT length = GetSystemDirectoryA(
        systemPath,
        sizeof(systemPath));

    if (length == 0 || length >= sizeof(systemPath))
    {
        fprintf(stderr,
                "[hook] failed to get System32 path\n");
        return;
    }

    std::filesystem::path path(systemPath);
    path /= "wsock32.dll";

    real_wsock32 = LoadLibraryA(
        path.string().c_str());

    if (!real_wsock32)
    {
        fprintf(stderr,
                "[hook] failed to load real wsock32.dll\n");
        return;
    }

    // WSOCK32 ordinal 4 = connect
    real_connect =
        reinterpret_cast<connect_fn>(
            GetProcAddress(
                real_wsock32,
                MAKEINTRESOURCEA(4)));

    if (!real_connect)
    {
        fprintf(stderr,
                "[hook] failed to resolve wsock32 ordinal 4\n");
    }
}

extern "C" int hooked_connect(
    SOCKET sockfd,
    const sockaddr *addr,
    int addrlen)
{
    if (!real_connect)
        resolve_connect();

    if (!real_connect)
        return SOCKET_ERROR;

    if (addr &&
        addrlen >= sizeof(sockaddr_in) &&
        addr->sa_family == AF_INET)
    {
        sockaddr_in modified =
            *reinterpret_cast<const sockaddr_in *>(addr);

        const char *ip = inet_ntoa(modified.sin_addr);

        unsigned short port =
            ntohs(modified.sin_port);

        printf(
            "[hook] connect(%s:%u)\n",
            ip,
            port);

        if (port == 80)
        {
            printf(
                "[hook] detected connection to http port, "
                "redirecting to %s:%u\n",
                g_ip.c_str(),
                g_port);

            in_addr destination{};

            destination.s_addr = inet_addr(g_ip.c_str());

            if (destination.s_addr == INADDR_NONE)
            {
                fprintf(stderr, "[hook] invalid destination IP: %s\n",
                        g_ip.c_str());
                return real_connect(sockfd, addr, addrlen);
            }

            modified.sin_addr = destination;

            modified.sin_port =
                htons(g_port);

            return real_connect(
                sockfd,
                reinterpret_cast<const sockaddr *>(&modified),
                sizeof(modified));
        }
    }

    return real_connect(
        sockfd,
        addr,
        addrlen);
}

extern "C" hostent *hooked_gethostbyname(const char *name)
{
    if (name)
    {
        printf("[hook] detected dns query, "
               "redirecting from \"%s\" to \"%s\"\n",
               name, g_ip.c_str());

        static in_addr addr;
        static char *addr_list[2];
        static hostent result;

        addr.s_addr = inet_addr(g_ip.c_str());

        addr_list[0] = reinterpret_cast<char *>(&addr);
        addr_list[1] = nullptr;

        result.h_name = const_cast<char *>(name);
        result.h_aliases = nullptr;
        result.h_addrtype = AF_INET;
        result.h_length = sizeof(addr);
        result.h_addr_list = addr_list;

        return &result;
    }

    return gethostbyname(name);
}

extern "C" servent *hooked_getservbyname(
    const char *name,
    const char *proto)
{
    return getservbyname(name, proto);
}

extern "C" int hooked_gethostname(char *name, int namelen)
{
    return gethostname(name, namelen);
}

BOOL APIENTRY DllMain(
    HMODULE hModule,
    DWORD reason,
    LPVOID reserved)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        DisableThreadLibraryCalls(hModule);

        load_config();
        resolve_connect();
    }

    return TRUE;
}