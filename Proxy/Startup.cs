using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Proxy.Config;
using Proxy.Repositories;

namespace Proxy
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            var credentials = new MqttCredentialsOptions(); 
            services.Configure<MqttCredentialsOptions>(Configuration.GetSection(nameof(MqttCredentialsOptions)));
            services.Configure<MqttServerOptions>(Configuration.GetSection(nameof(MqttServerOptions)));
            services.Configure<MqttTopicsOptions>(Configuration.GetSection(nameof(MqttTopicsOptions)));
            services.Configure<CosmosDbOptions>(Configuration.GetSection(nameof(CosmosDbOptions)));
            services.AddHostedService<MqttClientHostedService>();
            services.AddTransient<ITelemetricDataRepository, TelemetricDataRepository>();
            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Proxy", Version = "v1" });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Proxy v1"));
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
