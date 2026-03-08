using Owin;
using System.Web.Http;

namespace Webhooks.LegacyEngine.Server;

public class Startup
{
    public void Configuration(IAppBuilder app)
    {
        var config = new HttpConfiguration();

        // Configurar ruteo attribute-based
        config.MapHttpAttributeRoutes();

        // Ruteo default
        config.Routes.MapHttpRoute(
            name: "DefaultApi",
            routeTemplate: "api/{controller}/{id}",
            defaults: new { id = RouteParameter.Optional }
        );

        // Limpiar el XML formatter para forzar JSON por defecto
        config.Formatters.Remove(config.Formatters.XmlFormatter);

        // Habilitar camelCase/PascalCase fallback (similar a .NET Core)
        var jsonFormatter = config.Formatters.JsonFormatter;
        jsonFormatter.SerializerSettings.Formatting = Newtonsoft.Json.Formatting.None;

        // BUGS OWIN SELF-HOST .NET 4.8:
        // Asegurar que OWIN encuentre este Controller aunque esté instanciado desde el WinForms (.exe externo)
        config.Services.Replace(typeof(System.Web.Http.Dispatcher.IAssembliesResolver), new CustomAssemblyResolver());

        // Habilitar CORS Globalmente para que Hoppscotch Web funcione sin error de Preflight Options (404/405)
        app.UseCors(Microsoft.Owin.Cors.CorsOptions.AllowAll);

        app.UseWebApi(config);
        config.EnsureInitialized();
    }
}

// Resuelve el problema donde OWIN levantado desde un EXE Winforms no encuentra Controllers
// en una DLL importada.
public class CustomAssemblyResolver : System.Web.Http.Dispatcher.DefaultAssembliesResolver
{
    public override System.Collections.Generic.ICollection<System.Reflection.Assembly> GetAssemblies()
    {
        var assemblies = base.GetAssemblies();
        var currentAssembly = System.Reflection.Assembly.GetExecutingAssembly();

        if (!assemblies.Contains(currentAssembly))
        {
            assemblies.Add(currentAssembly);
        }
        return assemblies;
    }
}
