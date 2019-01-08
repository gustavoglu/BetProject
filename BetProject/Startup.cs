using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BetProject.Configurations;
using BetProject.Infra.Repositories;
using BetProject.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BetProject
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
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            var seleniumConfigurations = new SeleniumConfiguration();
            new ConfigureFromConfigurationOptions<SeleniumConfiguration>(
                configuration.GetSection("SeleniumConfiguration"))
                    .Configure(seleniumConfigurations);
            TelegramService ts = new TelegramService();
            PrincipalService ps = new PrincipalService();
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
            try
            {
                ts.EnviaMensagemParaOGrupo("App Iniciado");

                
               // ps.SalvaJogosAmanha(2,false).GetAwaiter().GetResult();
                ps.Iniciar(2,false).GetAwaiter().GetResult();
            }
            catch(Exception e)
            {
                GC.Collect();
                Console.WriteLine("Erro: " + e.Message + " - " + e.StackTrace);
                ps.Dispose();
                ts.EnviaMensagemParaOGrupo("Erro App : " + e.Message);
            }

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseMvc();

        }
    }
}
