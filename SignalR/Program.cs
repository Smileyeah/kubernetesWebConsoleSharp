using k8s;
using SignalR.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddSignalR();


// Load kubernetes configuration
var config =
    await KubernetesClientConfiguration.BuildConfigFromConfigFileAsync(new FileInfo("config/config"), "AliCloud");

// Register Kubernetes client interface as singleton
builder.Services.AddSingleton<IKubernetes>(_ => new Kubernetes(config));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromMinutes(2)
});
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/ws")
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            var kubernetes = context.RequestServices.GetRequiredService<IKubernetes>();
            
            await SignalR.WebSocketEcho.Echo(kubernetes, webSocket, context.Request.Query["workload"], context.RequestAborted);
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }
    else
    {
        await next(context);
    }

});

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();
app.MapHub<KubernetesHub>("/k8sHub");

app.Run();
