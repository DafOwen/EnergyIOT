using Microsoft.Extensions.Configuration;
using EnergyIOT.Models;
using EnergyIOT.Devices;
using EnergyIOT.DataAccess;
using Microsoft.Extensions.DependencyInjection;

namespace EnergyIOTDataSetup
{
    internal class Program
    {
        static async Task Main(string[] args)
        {

            //Config
            var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false);

            var config = builder.Build();
            var myDBConfig = config.GetSection("DatabaseConfig").Get<DatabaseConfig>();

            if (myDBConfig == null)
            {
                return;
            }

            #region IHttpClientFactory
            //Set up IHttpClientFactory ----------------------
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddHttpClient("kasaAPI", x =>
            {
                x.DefaultRequestHeaders.Accept.Clear();
                x.DefaultRequestHeaders.Add("Accept", "application/json");
            });
            serviceCollection.AddHttpClient("tapoAPI", x =>
            {
                x.DefaultRequestHeaders.Accept.Clear();
                x.DefaultRequestHeaders.Add("Accept", "application/json");
            })
            .ConfigurePrimaryHttpMessageHandler(() =>
            {
                // Allowing Untrusted SSL Certificates
                var handler = new HttpClientHandler();
                handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                handler.ServerCertificateCustomValidationCallback =
                    (httpRequestMessage, cert, cetChain, policyErrors) => true;

                return handler;
            });

            var serviceProvider = serviceCollection.BuildServiceProvider();

            var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();

            #endregion


            try
            {
                foreach (string arg in args)
                {
                    switch (arg.ToUpper())
                    {
                        case "/DB": //Database
                            try
                            {
                                DataSetup.PopulateCosmosBasics(myDBConfig).GetAwaiter().GetResult();
                            }
                            catch (Exception err)
                            {
                                Console.WriteLine("PopulateCosmosBasics Err:", err);
                            }
                            break;

                        case "/KF":
                            //Kasa Authenticate - First
                            ////MUST SWITCH OFF 2FA
                            try
                            {
                                var kasaAuthConfig = config.GetSection("KasaAuthConfig").Get<DeviceAuthConfig>();

                                DataStoreCosmoDB dataStoreCosmoDB = new();
                                dataStoreCosmoDB.Config(myDBConfig);
                                TPLinkKasa tpLinkKasa = new(dataStoreCosmoDB, httpClientFactory);
                                tpLinkKasa.DataConfig(myDBConfig);
                                tpLinkKasa.AuthenticateFirst(kasaAuthConfig).GetAwaiter().GetResult();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("KasaFIrst Authentication Error:{0}", ex.Message);
                            }
                            break;

                        case "/KR":

                            var kasaAuthConfigRefresh = config.GetSection("KasaAuthConfig").Get<DeviceAuthConfig>();
                            try
                            {
                                DataStoreCosmoDB dataStoreCosmoDBRefresh = new();
                                dataStoreCosmoDBRefresh.Config(myDBConfig);
                                TPLinkKasa tpLinkKasaRefresh = new(dataStoreCosmoDBRefresh, httpClientFactory);
                                tpLinkKasaRefresh.DataConfig(myDBConfig);
                                tpLinkKasaRefresh.AuthenticateRefreshToken(kasaAuthConfigRefresh).GetAwaiter().GetResult();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("KasaRefresh Error:{0}", ex.Message);
                            }
                            break;

                        case "/TF":
                            //Tapo Authenticate - First
                            ////MUST SWITCH OFF 2FA
                            try
                            {
                                var tapoAuthConfig = config.GetSection("TapoAuthConfig").Get<DeviceAuthConfig>();

                                DataStoreCosmoDB dataStoreCosmoDB = new();
                                dataStoreCosmoDB.Config(myDBConfig);
                                TPLinkTapo tpLinkTapo = new(dataStoreCosmoDB, httpClientFactory);
                                tpLinkTapo.DataConfig(myDBConfig);
                                tpLinkTapo.AuthenticateFirst(tapoAuthConfig).GetAwaiter().GetResult();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("TapoFirst Authentication Error:{0}", ex.Message);
                            }
                            break;

                        case "/TR":

                            var tapoAuthConfigRefresh = config.GetSection("TapoAuthConfig").Get<DeviceAuthConfig>();
                            try
                            {
                                DataStoreCosmoDB dataStoreCosmoDBRefresh = new();
                                dataStoreCosmoDBRefresh.Config(myDBConfig);
                                TPLinkTapo tpLinkTapoRefresh = new(dataStoreCosmoDBRefresh, httpClientFactory);
                                tpLinkTapoRefresh.DataConfig(myDBConfig);
                                tpLinkTapoRefresh.AuthenticateRefreshToken(tapoAuthConfigRefresh).GetAwaiter().GetResult();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("TapoRefresh Error:{0}", ex.Message);
                            }
                            break;

                        default:
                            Console.WriteLine("Options (1 at a time):");
                            Console.WriteLine("/db - Database: Populate Cosmos Database Basics");
                            Console.WriteLine("/kf - Kasa - First Authentication (2FA must be off)");
                            Console.WriteLine("/kr - Kasa - Authentication Refresh");
                            break;
                    }

                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

    }
}
