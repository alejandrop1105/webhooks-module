using System;
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
            // Leer el body crudo como string
            string rawPayload = await Request.Content.ReadAsStringAsync();

            // Si viene vacío o inválido
            if (string.IsNullOrWhiteSpace(rawPayload))
            {
                return BadRequest("Payload empty or invalid.");
            }

            // Notificamos al motor central (dispara el evento visual hacia el WinForms)
            WebhookHost.Instance.TriggerWebhookReceived(source, rawPayload);

            return Ok(new { success = true, message = "Webhook received internally via LegacyEngine" });
        }
        catch (Exception ex)
        {
            WebhookHost.Instance.TriggerWebhookError(source, ex.Message);
            return InternalServerError(ex);
        }
    }
}
