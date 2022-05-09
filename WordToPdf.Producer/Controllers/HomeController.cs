using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using RabbitMQ.Client;
using System.Diagnostics;
using System.Text;
using WordToPdf.Producer.Models;

namespace WordToPdf.Producer.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration _configuration;

        public HomeController(ILogger<HomeController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult WordToPdfPage()
        {
            return View();
        }

        [HttpPost]
        public IActionResult WordToPdfPage(Models.WordToPdf wordToPdf)
        {
            try
            {
                var factory = new ConnectionFactory();

                factory.Uri = new Uri(_configuration["ConnectionStrings:RabbitMQCloudString"]);

                using (var connection = factory.CreateConnection())
                {
                    using (var channel = connection.CreateModel())
                    {
                        channel.ExchangeDeclare("convert-exchange", ExchangeType.Direct, true, false, null);

                        channel.QueueDeclare(queue: "File", durable: true, exclusive: false, autoDelete: false, arguments: null);

                        channel.QueueBind(queue: "File", exchange: "convert-exchange", routingKey: "WordToPdf");

                        MessageWordToPdf messageWordToPdf = new MessageWordToPdf();

                        using (MemoryStream ms = new MemoryStream())
                        {
                            wordToPdf.WordFile.CopyTo(ms);
                            messageWordToPdf.WordByte = ms.ToArray();
                        }

                        messageWordToPdf.Email = wordToPdf.Email;
                        messageWordToPdf.FileName = Path.GetFileNameWithoutExtension(wordToPdf.WordFile.FileName);

                        string serializeMessage = JsonConvert.SerializeObject(messageWordToPdf);

                        byte[] byteMessage = Encoding.UTF8.GetBytes(serializeMessage);

                        var properties = channel.CreateBasicProperties();

                        //rabbitmq instance restart atması durumunda mesajları korur.
                        properties.Persistent = true;

                        channel.BasicPublish("convert-exchange", routingKey: "WordToPdf", basicProperties: properties, body: byteMessage);

                        ViewBag.result = " Word dosyanı pdf dosyasına dönüştürüldükten sonra size e-mail olarak gönderilecektir.";

                        return View();
                    }
                }
            }
            catch (Exception ex)
            {

                throw ex;
            }

        }


        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}