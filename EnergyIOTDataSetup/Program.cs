using Microsoft.Extensions.Configuration;

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


            try
            {
                foreach (string arg in args)
                {
                    switch (arg.ToUpper())
                    {
                        case "/DB": //Database
                            DataSetup.PopulateCosmosBasics(myDBConfig).GetAwaiter().GetResult();
                            break;

                        case "/KF"://Kasa Authenticate - First
                            KasaAuthenticate kasaAuthenticateFirst = new();
                            //MUST SWITCH OFF 2FA
                            kasaAuthenticateFirst.AuthenticateKasaFirst(myDBConfig).GetAwaiter().GetResult();
                            break;

                        case "/KR":
                            KasaAuthenticate kasaAuthenticateRefresh = new();
                            kasaAuthenticateRefresh.AuthenticateKasaRefresh(myDBConfig).GetAwaiter().GetResult();
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
