using System.Diagnostics;
using System.Runtime.InteropServices;
using FFTModLoader.ContentExpansion;
using FFTModLoader.ContentExpansion.Runtime;
using Reloaded.Mod.Interfaces;
using Reloaded.Mod.Interfaces.Internal;
using IReloadedHooks = Reloaded.Hooks.ReloadedII.Interfaces.IReloadedHooks;

namespace ReworkedChemist.VenomTest;

public sealed class Startup:IMod
{
    private IReloadedHooks? _hooks;
    private ILogger? _logger;
    private ChemistPlayableHost? _host;
    private StreamWriter? _file;
    private readonly object _logGate=new();
    private string _directory="";
    private bool _started;
    public void StartEx(IModLoaderV1 api,IModConfigV1 config)
    {
        var loader=(IModLoader)api;_logger=(ILogger)loader.GetLogger();
        _directory=loader.GetDirectoryForModId(config.ModId);
        loader.GetController<IReloadedHooks>()?.TryGetTarget(out _hooks!);
        loader.OnModLoaderInitialized+=Initialize;
    }
    private void Initialize()
    {
        if(_started)return;_started=true;
        try
        {
            var module=Process.GetCurrentProcess().MainModule??throw new InvalidOperationException("Game module missing.");
            if(!module.ModuleName.Equals("FFT_enhanced.exe",StringComparison.OrdinalIgnoreCase))throw new NotSupportedException("Enhanced game only.");
            if(_hooks is null)throw new InvalidOperationException("Native hook dependency unavailable.");
            string logs=Path.Combine(Path.GetDirectoryName(module.FileName)!,"FFTModLoader.Runtime","Logs");Directory.CreateDirectory(logs);
            string path=Path.Combine(logs,$"Chemist-Venom-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Environment.ProcessId}.txt");
            _file=new StreamWriter(new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read)){AutoFlush=true};
            var bridge=new NativeContextBridge(Path.Combine(_directory,"FFTModLoader.ContentExpansion.Native.dll"));
            bridge.EnableDiagnosticCapture(Path.Combine(logs,$"Chemist-FirstFault-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Environment.ProcessId}"));
            Log("[Diagnóstico] Captura nativa v2: até 16 registros de contexto/código/pilha, dez regiões apontadas de até 2 KB e histórico circular das gravações protegidas do Chemist. Não captura todo o heap nem suprime exceções.");
            _host=new ChemistPlayableHost(_hooks,(long)module.BaseAddress,bridge,Log);
            _host.Install(module.FileName,_directory,Profile);
            Log("[Teste] Aprender Venom Flask por 70 JP; comprar por 140 gil; usar Poison em um alvo a até 4 casas; conferir estoque antes/depois e salvar/recarregar.");
            Log("[Teste] v0.2.43 VOLTAR APÓS ESCOLHER ITEM: limpeza completa nativa de Items e fechamento somente da raiz preservam a seleção interna ao cancelar alvo e voltar. Regressão nativa reproduzida;48 ciclos completos e192+192 saídas verificados em fixtures. Ordem/posição das quatro janelas preservada; confirmação em batalha pendente. Fire/Ice/Thunder =5 +15% HP máximo arredondado para cima; alvo150HP =28 antes dos modificadores elementais.");
            Log("[Save] Extras armazenados separadamente. Preserve também a pasta ExpandedSaves ao copiar saves para outro computador.");
        }
        catch(Exception ex){Log("[Erro] Integração Venom não foi ativada: "+ex);}
    }
    private void Log(string line)
    {
        lock(_logGate)
        {
            string message="[ReworkedChemist] "+line;
            try{_file?.WriteLine($"{DateTime.UtcNow:O} {message}");}catch{}
            // The installed 0.11.6 live channel selects "modded" content and
            // relays RuntimeSession/Error markers. Reuse that contract without
            // replacing the approved launcher or writing through a competing
            // file handle into its session stream.
            string channel=line.StartsWith("[Erro]",StringComparison.Ordinal)?"[Error] ":"[RuntimeSession] modded content: ";
            try{_logger?.WriteLine(channel+message);}catch{}
        }
    }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]private static extern nint GetModuleHandle(string name);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate nint SteamUser();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate ulong SteamId(nint self);
    private static string Profile()
    {
        nint library=GetModuleHandle("steam_api64.dll");
        if(library==0)throw new InvalidDataException("Steam profile library is not initialized.");
        var get=Marshal.GetDelegateForFunctionPointer<SteamUser>(NativeLibrary.GetExport(library,"SteamAPI_SteamUser_v023"));
        nint user=get();if(user==0)throw new InvalidDataException("Steam user is not initialized at the save boundary.");
        ulong id=Marshal.GetDelegateForFunctionPointer<SteamId>(NativeLibrary.GetExport(library,"SteamAPI_ISteamUser_GetSteamID"))(user);
        if(id==0)throw new InvalidDataException("Native Steam profile is unavailable.");
        return ExpandedSaveRegistry.Hash(BitConverter.GetBytes(id)); // never log account identity
    }
    public bool CanUnload()=>false;public bool CanSuspend()=>false;
    public void Suspend(){}public void Resume(){}public void Unload(){}
    public Action Disposing=>()=>{};
}
