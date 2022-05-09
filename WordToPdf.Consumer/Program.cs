
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Spire.Doc;
using System.Net;
using System.Net.Mail;
using System.Text;
using WordToPdf.Consumer;

static bool EmailSend(string email, MemoryStream memoryStream, string fileName)
{
    if (fileName == null || email == null) return false;
    try
    {
        memoryStream.Position = 0;

        //attachment tipi pdf
        System.Net.Mime.ContentType ct = new System.Net.Mime.ContentType(System.Net.Mime.MediaTypeNames.Application.Pdf);

        Attachment attach = new Attachment(memoryStream, ct);
        attach.ContentDisposition.FileName = $"{fileName}.pdf";

        MailMessage mailMessage = new MailMessage();

        mailMessage.From = new MailAddress("admin@teknohub.net");
        mailMessage.To.Add(email);
        mailMessage.Subject = "Pdf Dosyası";
        mailMessage.Body = "Pdf dosyanız ektedir.";
        mailMessage.IsBodyHtml = true;
        mailMessage.Attachments.Add(attach);

        SmtpClient smtpClient = new SmtpClient();
        smtpClient.Host = "mail.teknohub.net";
        smtpClient.Port = 587;
        smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
        smtpClient.Credentials = new NetworkCredential("admin@teknohub.net", "Fatih1234");
        smtpClient.UseDefaultCredentials = false;
        smtpClient.EnableSsl = true;
        smtpClient.SendMailAsync(mailMessage);

        Console.WriteLine($"Sonuç: {email} adresine gönderilmiştir.");
        memoryStream.Close();
        memoryStream.Dispose();

        return true;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Mail gönderim sırasında bir hata meydana geldi {ex.InnerException}");
        return false;
    }
}

bool result = false;

var factory = new ConnectionFactory();

factory.Uri = new Uri("amqps://jkqvnpvo:eXkyQt9XWMfL4QjfIenTHOOc3PDRUC9q@clam.rmq.cloudamqp.com/jkqvnpvo");

using (var connection = factory.CreateConnection())
{
    using (var channel = connection.CreateModel())
    {
        channel.ExchangeDeclare("convert-exchange", ExchangeType.Direct, true, false, null);

        channel.QueueDeclare(queue: "File", durable: true, exclusive: false, autoDelete: false, arguments: null);

        channel.QueueBind(queue:"File", exchange:"convert-exchange",routingKey:"WordToPdf");

        //mesajların eşit bir şekilde dağılması için kullanırız.
        channel.BasicQos(0,1,false);

        var consumer = new EventingBasicConsumer(channel);

        channel.BasicConsume(queue:"File",autoAck:false,consumer:consumer);

        //dinliyoruz:
        consumer.Received += (model, ea) =>
        {
            try
            {
                Console.WriteLine("Kuyruktan bir mesaj alındı ve işleniyor");

                Document document = new Document();

                string message = Encoding.UTF8.GetString(ea.Body.ToArray());
                MessageWordToPdf messageWordToPdf = JsonConvert.DeserializeObject<MessageWordToPdf>(message);

                document.LoadFromStream(stream: new MemoryStream(messageWordToPdf.WordByte),fileFormat:FileFormat.Docx2013);

                using (MemoryStream ms = new MemoryStream())
                {
                    document.SaveToStream(stream: ms, fileFormat: FileFormat.PDF);

                    result = EmailSend(email: messageWordToPdf.Email, memoryStream: ms, fileName: messageWordToPdf.FileName);
                }

            }
            catch (global::System.Exception ex)
            {
                Console.WriteLine("Hata meydana geldi: " + ex.Message);
                throw ex;
            }


            if(result is true)
            {
                Console.WriteLine("Kuyrukta mesaj başarıyla işlendi..");

                //başarılı ise burası kuyruktan siler.
                channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
            }
        };

        Console.WriteLine("Çıkmak için tıklayınız..");
        Console.ReadLine();
    }
}