using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
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
using System.Web.UI.WebControls;

namespace AppScenario2
{
    public partial class Form1 : Form
    {

        string baseURI = @"http://localhost:5676";


        RestClient client = null;
        MqttClient clientMqtt = null;
        string topicToSubscribe = "";

        public Form1()
        {
            InitializeComponent();
            client = new RestClient(baseURI);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var somiod_locate = "";

            if (string.IsNullOrEmpty(urlGet.Text))
            {
                MessageBox.Show("URL cannot be null or empty.", "Input Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (appRadioGet.Checked)
            {
                somiod_locate = "application";
            }
            else if (contRadioGet.Checked)
            {
                somiod_locate = "container";
            }
            else if (recRadioGet.Checked)
            {
                somiod_locate = "record";
            }
            else if (notifRadioGet.Checked)
            {
                somiod_locate = "notification";
            }

            var request = new RestRequest(urlGet.Text, Method.Get);
            request.AddHeader("Accept", "application/xml");
            if (somiod_locate != "")
            {
                request.AddHeader("somiod-locate", somiod_locate);
            }

            try
            {
                var response = client.Execute(request);
                if (response.IsSuccessful)
                {
                    richTextBox1.Clear();
                    richTextBox1.AppendText(response.Content);
                }
                else
                {
                    MessageBox.Show($"Resource not found or an error occurred. Code {response.StatusCode}\n");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Request Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private void deleteButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(deletePath.Text))
            {
                MessageBox.Show("Path cannot be null or empty.", "Input Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var request = new RestRequest(deletePath.Text, Method.Delete);

            try
            {
                var response = client.Execute(request);
                if (response.IsSuccessful)
                {
                    MessageBox.Show("Resource deleted successfully.\n");
                }
                else
                {
                    MessageBox.Show($"Resource not found or an error occurred. Code {response.StatusCode}\n");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Request Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void subscribeButton_Click(object sender, EventArgs e)
        {
            // Check if the input is null or empty
            if (string.IsNullOrWhiteSpace(urlText.Text))
            {
                MessageBox.Show("URL cannot be null or empty.", "Input Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(topicText.Text))
            {
                MessageBox.Show("Topic cannot be null or empty.", "Input Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            topicToSubscribe = topicText.Text;

            clientMqtt = new MqttClient(urlText.Text);
            try
            {
                clientMqtt.Connect(Guid.NewGuid().ToString());

                if (clientMqtt.IsConnected)
                {
                    clientMqtt.Subscribe(new string[] {
                        topicText.Text
                    },
                    new byte[] {
                        MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE
                    });

                    MessageBox.Show("Connected to the broker " + urlText.Text + "\n topic: " + topicText.Text);

                    clientMqtt.MqttMsgPublishReceived += client_MqttMsgPublishReceived;

                }
                else
                {
                    MessageBox.Show("Connection failed.", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception)
            {

                MessageBox.Show("Error while connecting to the broker. Check the url and topic.", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
           
        }

        static void client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            
            MessageBox.Show("Received = " + Encoding.UTF8.GetString(e.Message) +
                " on topic " + e.Topic);
        }





        private void button6_Click(object sender, EventArgs e)
        {
            if (clientMqtt != null && clientMqtt.IsConnected)
            {
                clientMqtt.Unsubscribe(new string[] { topicToSubscribe });
                clientMqtt.Disconnect();
                clientMqtt = null;
            }

            MessageBox.Show("Disconnected from the MQTT broker.", "Disconnected", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void button6_Click_1(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(urlPut.Text))
            {
                MessageBox.Show("URL cannot be null or empty.", "Input Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrEmpty(namePut.Text))
            {
                MessageBox.Show("Name cannot be null or empty.", "Input Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }


            XmlDocument doc = new XmlDocument();

            XmlElement nameElement = doc.CreateElement("name");
            nameElement.InnerText = namePut.Text;
            XmlElement root = doc.CreateElement(appButton.Checked ? "Application" : "Container");

            root.AppendChild(nameElement);

            var putRequest = new RestRequest(urlPut.Text, Method.Put);
            putRequest.AddBody(root.OuterXml);
            putRequest.AddHeader("Content-Type", "application/xml");
            putRequest.AddHeader("res-type", appButton.Checked ? "application" : "container");

            try
            {
                var response = client.Execute(putRequest);
                if (response.IsSuccessful)
                {
                    MessageBox.Show("Resource updated successfully.\n");
                }
                else
                {
                    MessageBox.Show($"Resource not found or an error occurred. Code {response.StatusCode}\n");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Request Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            var resource_type = "";

            if (appRadio.Checked)
            {
                resource_type = "application";
            }
            else if (contRadio.Checked)
            {
                resource_type = "container";
            }
            else if (recRadio.Checked)
            {
                resource_type = "record";
            }
            else if (notifRadio.Checked)
            {
                resource_type = "notification";
            }
            else
            {
                MessageBox.Show("Please select a resource type.", "Input Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrEmpty(urlPost.Text))
            {
                MessageBox.Show("URL cannot be null or empty.", "Input Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            XmlDocument doc = new XmlDocument();

            XmlElement root = doc.CreateElement(char.ToUpper(resource_type[0]) + resource_type.Substring(1));
            
            XmlElement nameElement = doc.CreateElement("name");
            nameElement.InnerText = namePost.Text;

            XmlElement contElement = doc.CreateElement("content");
            contElement.InnerText = contPost.Text;

            XmlElement endElement = doc.CreateElement("endpoint");
            endElement.InnerText = endPost.Text;

            XmlElement evElement = doc.CreateElement("event");
            evElement.InnerText = evPost.Text;

            XmlElement enaElement = doc.CreateElement("enabled");
            enaElement.InnerText = enaPost.Checked.ToString().ToLower();

            root.AppendChild(nameElement);
            root.AppendChild(contElement);
            root.AppendChild(endElement);
            root.AppendChild(evElement);
            root.AppendChild(enaElement);

            var request = new RestRequest(urlPost.Text, Method.Post);

            request.AddHeader("Content-Type", "application/xml");
            request.AddHeader("res-type", resource_type);
            request.AddBody(root.OuterXml);

            try
            {
                var response = client.Execute(request);
                if (response.IsSuccessful)
                {
                    MessageBox.Show("Resource created successfully.\n");
                }
                else
                {
                    MessageBox.Show($"Resource not found or an error occurred. Code {response.StatusCode}\n");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Request Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void cleatButton_Click(object sender, EventArgs e)
        {
            appRadioGet.Checked = false;
            contRadioGet.Checked = false;
            recRadioGet.Checked = false;
            notifRadioGet.Checked = false;
        }
    }
}
