using System.Runtime.InteropServices;
using Microsoft.Windows.Widgets.Feeds.Providers;
using MisWidgets.Feed.Core;
using MisWidgets.Feed.Provider.Diagnostics;

namespace MisWidgets.Feed.Provider;

[ComVisible(true)]
[ComDefaultInterface(typeof(IFeedProvider))]
[Guid(ProviderConstants.ClassId)]
public sealed class FeedProvider : IFeedProvider, IFeedProviderMessage
{
    public void OnFeedProviderEnabled(FeedProviderEnabledArgs args)
    {
        string providerId = args.FeedProviderDefinitionId;
        LocalLog.Info($"Provider habilitado: {providerId}.");
        UpdateQueryParameters(providerId);
    }

    public void OnFeedProviderDisabled(FeedProviderDisabledArgs args)
    {
        string providerId = args.FeedProviderDefinitionId;
        LocalLog.Info($"Provider deshabilitado: {providerId}.");

        // Este paquete registra un único provider. El contrato solo invoca este callback
        // cuando todos sus feeds están deshabilitados, por lo que ya no queda trabajo servido.
        ProviderProcess.RequestStop();
    }

    public void OnFeedEnabled(FeedEnabledArgs args)
    {
        LocalLog.Info($"Feed habilitado: {args.FeedDefinitionId}.");
    }

    public void OnFeedDisabled(FeedDisabledArgs args)
    {
        LocalLog.Info($"Feed deshabilitado: {args.FeedDefinitionId}.");
    }

    public void OnCustomQueryParametersRequested(CustomQueryParametersRequestedArgs args)
    {
        string providerId = args.FeedProviderDefinitionId;
        LocalLog.Info($"Renovacion de parametros solicitada: {providerId}.");
        UpdateQueryParameters(providerId);
    }

    public void OnMessageReceived(FeedMessageReceivedArgs args)
    {
        // Los objetos callback solo son validos durante esta llamada. Copiamos de inmediato
        // todos sus valores y nunca retenemos args.
        string providerId = args.FeedProviderDefinitionId;
        string feedId = args.FeedDefinitionId;
        string message = args.Message;

        if (!string.Equals(providerId, FeedIdentifiers.FeedProviderId, StringComparison.Ordinal) ||
            !string.Equals(feedId, FeedIdentifiers.FeedDefinitionId, StringComparison.Ordinal))
        {
            LocalLog.Warn($"Mensaje descartado para IDs no registrados: provider={providerId}; feed={feedId}.");
            return;
        }

        try
        {
            BridgeParseResult parsed = BridgeProtocol.ParseRequest(message);
            string response = parsed.IsSuccess
                ? BridgeProtocol.CreatePong(parsed.Request!.RequestId, DateTimeOffset.UtcNow)
                : parsed.ErrorResponse!;

            FeedManager.GetDefault().SendMessageToContent(providerId, feedId, response);
            LocalLog.Info(parsed.IsSuccess
                ? $"diagnostics.pong enviado para {parsed.Request!.RequestId:D}."
                : "diagnostics.error enviado por solicitud no valida.");
        }
        catch (Exception exception)
        {
            LocalLog.Error("Fallo al procesar un mensaje del contenido web.", exception);
        }
    }

    private static void UpdateQueryParameters(string providerId)
    {
        if (!string.Equals(providerId, FeedIdentifiers.FeedProviderId, StringComparison.Ordinal))
        {
            LocalLog.Warn($"No se actualizaron parametros para el provider desconocido '{providerId}'.");
            return;
        }

        var options = new CustomQueryParametersUpdateOptions(
            providerId,
            ProviderConstants.QueryParameters);
        FeedManager.GetDefault().SetCustomQueryParameters(options);
    }
}
