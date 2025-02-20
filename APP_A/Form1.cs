using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Serialization;
using System.Xml;
using RestSharp;
using RestSharp.Serializers;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace APP_A
{
    public partial class Form1 : Form
    {
        string statusApp = "off";

        string baseURL = @"http://localhost:5676";

        public Form1()
        {
            InitializeComponent();
            RestClient client = new RestClient(baseURL);

            try
            {
                RestRequest request_application = new RestRequest("api/somiod/Lighting", Method.Get);
                request_application.AddHeader("Accept", "application/xml");
                var response = client.Execute(request_application);
                if (!response.IsSuccessful)
                {
                    RestRequest request = new RestRequest("api/somiod", Method.Post);
                    request.AddHeader("res-type", "application");
                    request.RequestFormat = DataFormat.Xml;
                    XmlDocument doc = new XmlDocument();
                    XmlElement application = doc.CreateElement("Application");
                    XmlElement name = doc.CreateElement("name");
                    name.InnerText = "Lighting";
                    application.AppendChild(name);
                    doc.AppendChild(application);
                    request.AddParameter("application/xml", doc.InnerXml, ParameterType.RequestBody);
                    try
                    {
                        client.Execute(request);
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                }
                VerifyContainer(client);
                VerifyNotification(client);
            }
            catch (Exception ex)
            {
                MessageBox.Show("An error occurred: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Form1.ActiveForm.Close();
            }


        }

        private void Form1_Load(object sender, EventArgs e)
        {
            MqttClient clientMqtt = new MqttClient("broker.hivemq.com");
            try
            {
                clientMqtt.Connect(Guid.NewGuid().ToString());

                if (clientMqtt.IsConnected)
                {
                    clientMqtt.Subscribe(new string[] {
                        "light_bulb"
                    },
                    new byte[] {
                        MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE
                    });
                    clientMqtt.MqttMsgPublishReceived += client_MqttMsgPublishReceived;

                }
                else
                {
                    MessageBox.Show("Connection failed.", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Form1.ActiveForm.Close();
                }
            }
            catch (Exception)
            {

                MessageBox.Show("Error while connecting to the broker. Check the url and topic.", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Form1.ActiveForm.Close();
            }

        }

        private void VerifyContainer(RestClient client)
        {
            RestRequest request_container = new RestRequest("api/somiod/Lighting/light_bulb", Method.Get);
            request_container.AddHeader("Accept", "application/xml");
            var response_container = client.Execute(request_container);
            if (!response_container.IsSuccessful)
            {
                RestRequest request_create_container = new RestRequest("api/somiod/Lighting", Method.Post);
                request_create_container.AddHeader("res-type", "container");
                request_create_container.RequestFormat = DataFormat.Xml;
                XmlDocument doc = new XmlDocument();
                XmlElement container = doc.CreateElement("Container");
                XmlElement name = doc.CreateElement("name");
                name.InnerText = "light_bulb";
                container.AppendChild(name);
                doc.AppendChild(container);
                request_create_container.AddParameter("application/xml", doc.InnerXml, ParameterType.RequestBody);
                try
                {
                    client.Execute(request_create_container);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("An error occurred: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Form1.ActiveForm.Close();
                }
            }
        }
        private void VerifyNotification(RestClient client)
        {
            RestRequest request = new RestRequest("api/somiod/Lighting/light_bulb/notification/bulb_status", Method.Get);
            request.AddHeader("Accept", "application/xml");
            try
            {
                var response = client.Execute(request);
                if (!response.IsSuccessful)
                {
                    RestRequest createNotificationRequest = new RestRequest("api/somiod/Lighting/light_bulb/notification", Method.Post);
                    createNotificationRequest.AddHeader("res-type", "notification");
                    createNotificationRequest.RequestFormat = DataFormat.Xml;
                    XmlDocument doc = new XmlDocument();
                    XmlElement notification = doc.CreateElement("Notification");
                    XmlElement name = doc.CreateElement("name");
                    name.InnerText = "bulb_status";
                    XmlElement endpoint = doc.CreateElement("endpoint");
                    endpoint.InnerText = "mqtt://broker.hivemq.com";
                    XmlElement @event = doc.CreateElement("event");
                    @event.InnerText = "0";
                    XmlElement enabled = doc.CreateElement("enabled");
                    enabled.InnerText = "true";
                    notification.AppendChild(name);
                    notification.AppendChild(endpoint);
                    notification.AppendChild(@event);
                    notification.AppendChild(enabled);
                    createNotificationRequest.AddParameter("application/xml", notification.OuterXml, ParameterType.RequestBody);
                    try
                    {
                        client.Execute(createNotificationRequest);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("An error occurred: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Form1.ActiveForm.Close();
                    }
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show("An error occurred: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Form1.ActiveForm.Close();
            }
        }

       private void client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            string received_content = (Encoding.UTF8.GetString(e.Message));
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(received_content);
            XmlNode contentNode = xmlDoc.SelectSingleNode("//content");
            string wrappedXml = $"<root>{contentNode.InnerText}</root>";
            xmlDoc.LoadXml(wrappedXml);
            XmlNode rootNode = xmlDoc.SelectSingleNode("/root");

            XmlDocument inner_data_doc = new XmlDocument();
            string cdata_string = rootNode.FirstChild.Value;
            inner_data_doc.LoadXml(cdata_string);

            XmlNode messageNode = inner_data_doc.SelectSingleNode("/cmd");
            string content = messageNode.InnerText;
            ChangeLampByStatus(content);
        }


        private void ChangeLampByStatus(string status)
        {

            if (status == "on")
            {
                pictureBox.Image = Properties.Resources.lamp_on;
                statusApp = "on";
            }
            else { 
                pictureBox.Image = Properties.Resources.lamp_off;
                statusApp = "off";
            }

        }

        private void pictureBox_Click(object sender, EventArgs e)
        {
        }
    }
}
