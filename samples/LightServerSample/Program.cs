using System;
using Webhooks.LightServer;

namespace LightServerSample;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== Servidor de Webhooks Ligero (sin SignalR) ===");

        // Configuración
        int port = 8080;
        string secretKey = "mi_clave_secreta"; // La misma que configuras en WooCommerce/GitHub

        Console.WriteLine($"Iniciando servidor en puerto {port}...");
        Console.WriteLine($"Endpoint esperado: http://localhost:{port}/webhook/{{source}}");
        Console.WriteLine("Presiona Enter para detener.");

        // Crear instancia del servidor
        using (var server = new SimpleWebhookServer(port, secretKey))
        {
            // Suscribirse a eventos
            server.OnLog += (sender, msg) => Console.WriteLine($"LOG: {msg}");

            server.OnWebhookReceived += (sender, e) =>
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n>>> WEBHOOK RECIBIDO <<<");
                Console.WriteLine($"Fuente: {e.Source}");
                Console.WriteLine($"Hora: {e.ReceivedAt}");
                Console.WriteLine("Headers:");
                foreach (var header in e.Headers)
                {
                    Console.WriteLine($"  {header.Key}: {header.Value}");
                }
                Console.WriteLine("Payload (truncado):");
                Console.WriteLine(e.Payload.Length > 200 ? e.Payload.Substring(0, 200) + "..." : e.Payload);
                Console.ResetColor();
            };

            // Iniciar
            try
            {
                server.Start();
                Console.ReadLine(); // Mantener corriendo
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error fatal: {ex.Message}");
                Console.WriteLine("Asegúrate de ejecutar como Administrador si usas puertos < 1024 o IPs específicas.");
            }
        }

        Console.WriteLine("Servidor detenido.");
    }
}
