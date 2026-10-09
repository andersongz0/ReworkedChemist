using FFTModLoader.ContentExpansion.Api;
using Reloaded.Mod.Interfaces;
using Reloaded.Mod.Interfaces.Internal;

namespace ReworkedChemist.VenomTest; // stable internal identity, not the public mod name

public sealed class Startup : IMod
{
    private IModLoader? _loader;
    private string _directory = "";
    private bool _started;
    public void StartEx(IModLoaderV1 api, IModConfigV1 config)
    {
        _loader = (IModLoader)api;
        _directory = _loader.GetDirectoryForModId(config.ModId);
        _loader.OnModLoaderInitialized += Initialize;
    }
    private void Initialize()
    {
        if (_started) return;
        _started = true;
        try
        {
            IContentExpansion? service = null;
            if (_loader!.GetController<IContentExpansion>()?.TryGetTarget(out service) != true || service is null)
                throw new InvalidOperationException("FFTModLoader ContentExpansion is missing. Install FFTModLoader 0.11.7 or newer.");
            if (service.ContractVersion != 1) throw new NotSupportedException("Unsupported ContentExpansion API version.");
            service.ActivateReworkedChemist(_directory);
        }
        catch (Exception ex)
        {
            if (_loader is not null)
                ((ILogger)_loader.GetLogger()).WriteLine("[Error] [ReworkedChemist] Activation refused: " + ex.Message);
        }
    }
    public bool CanUnload() => false;
    public bool CanSuspend() => false;
    public void Suspend() { }
    public void Resume() { }
    public void Unload() { }
    public Action Disposing => () => { };
}
