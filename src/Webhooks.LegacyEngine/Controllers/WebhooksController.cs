using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Newtonsoft.Json.Linq;
using Webhooks.LegacyEngine.Engine;

namespace Webhooks.LegacyEngine.Controllers;

[RoutePrefix("api/webhooks")]
public class WebhooksController : ApiController
{
    [HttpGet]
    [Route("ping")]
    public IHttpActionResult Ping()
    {
        return Ok("pong");
    }

    [HttpPost]
    [Route("{source}")]
    public async Task<IHttpActionResult> ReceiveWebhook(string source)
    {
        try
        {
            source = source?.Trim();
            if (string.IsNullOrEmpty(source))
            {
                return BadRequest("Source vacio.");
            }

            // Leer el body crudo como string
            string rawPayload = await Request.Content.ReadAsStringAsync();

            // Si viene vacío o inválido
            if (string.IsNullOrWhiteSpace(rawPayload))
            {
                return BadRequest("Payload empty or invalid.");
            }

            // Capturar headers (request + content) en un dict case-insensitive.
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var h in Request.Headers)
                headers[h.Key] = string.Join(",", h.Value ?? Enumerable.Empty<string>());
            if (Request.Content?.Headers != null)
            {
                foreach (var h in Request.Content.Headers)
                    headers[h.Key] = string.Join(",", h.Value ?? Enumerable.Empty<string>());
            }

            // Procesar via Processor registrado y mapear resultado a HTTP status.
            // Si no hay processor registrado, el host devuelve Processed (200) por compat.
            var result = await WebhookHost.Instance.TriggerWebhookProcessAsync(source, rawPayload, headers);

            switch (result)
            {
                case WebhookProcessResult.Processed:
                    return Ok(new { success = true, message = "Procesado." });

                case WebhookProcessResult.NoProvider:
                    // 404: no hay handler para este source, el sender NO debe reintentar
                    return ResponseMessage(Request.CreateResponse(HttpStatusCode.NotFound, new
                    {
                        success = false,
                        message = $"No hay provider registrado para source '{source}'."
                    }));

                case WebhookProcessResult.NotProcessed:
                default:
                    // 503: el provider existe pero no pudo procesar (manager apagado, error transitorio, etc.).
                    // El sender DEBE reintentar.
                    return ResponseMessage(Request.CreateResponse(HttpStatusCode.ServiceUnavailable, new
                    {
                        success = false,
                        message = $"Webhook recibido pero no procesado para source '{source}'. Reintente."
                    }));
            }
        }
        catch (Exception ex)
        {
            WebhookHost.Instance.TriggerWebhookError(source, ex.Message);
            return InternalServerError(ex);
        }
    }
}
