using System;using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Windows.Forms;
using RestSharp;
using System.Web.Script.Serialization;
using System.Xml;

namespace AppB
{
    public partial class Form1 : Form
    {
        string baseURI = @"http://localhost:5676/"; //TODO: needs to be updated!
        RestClient client = null;
        public Form1()
        {
            InitializeComponent();
            client = new RestClient(baseURI);

            App switchLight = new App
            {
                name = "switch",
            };

            var request = new RestRequest("api/somiod", Method.Post);
            request.RequestFormat = DataFormat.Xml;
            request.AddObject(switchLight);

            var response = client.Execute(request);
            if (response.StatusCode == HttpStatusCode.OK)
            {

                MessageBox.Show("App Created");

            }
            else
            {
                MessageBox.Show("App already got Created");
            }
        }

        private void light_on_Click(object sender, EventArgs e)
        {
            // Ensure you have the RestSharp library installed
            var client = new RestClient("http://localhost:5676/"); // Replace with your API's base URL

            // Create the REST request
            var request = new RestRequest("api/somiod/Lighting/light_bulb/record", Method.Post);
            request.AddHeader("res-type", "record"); // Add the required header
            request.RequestFormat = DataFormat.Xml;

            // Correct Record object initialization
            var record = new Record
            {
                name = Guid.NewGuid().ToString(),
                content = "<![CDATA[<cmd>on</cmd>]]>"
            };

            // Add the Record object to the request body
            request.AddXmlBody(record);

            try
            {
                // Execute the request
                var response = client.Execute(request);

                if (response.IsSuccessful)
                {
                    MessageBox.Show("Record added successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show($"Error: {response.StatusCode} - {response.Content}", "Request Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                // Display error message
                MessageBox.Show($"Error: {ex.Message}", "Request Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void light_off_Click(object sender, EventArgs e)
        {
            // Ensure you have the RestSharp library installed
            var client = new RestClient("http://localhost:5676/"); // Replace with your API's base URL

            // Create the REST request
            var request = new RestRequest("api/somiod/Lighting/light_bulb/record", Method.Post);
            request.AddHeader("res-type", "record"); // Add the required header
            request.RequestFormat = DataFormat.Xml;

            // Correct Record object initialization
            var record = new Record
            {
                name = Guid.NewGuid().ToString(),
                content = "<![CDATA[<cmd>off</cmd>]]>"
            };

            // Add the Record object to the request body
            request.AddXmlBody(record);

            try
            {
                // Execute the request
                var response = client.Execute(request);

                if (!(response.IsSuccessful))
                {
                    MessageBox.Show($"Error: {response.StatusCode} - {response.Content}", "Request Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                // Display error message
                MessageBox.Show($"Error: {ex.Message}", "Request Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }
    }
}
