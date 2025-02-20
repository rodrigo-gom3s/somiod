using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Web.Http;
using MySql.Data.MySqlClient;
using SOMIOD.Models;
using uPLibrary.Networking.M2Mqtt;
using RestSharp;
using System.Xml;


namespace SOMIOD.Controllers
{
    public class SomiodController : ApiController
    {
        // Retrieve the connection string from the configuration file
        protected string connectionString = ConfigurationManager.ConnectionStrings["MySqlConnection"].ConnectionString;

        // this method is used to check if the name is unique in the entire db
        protected bool checkUniqueName(string name, MySqlConnection conn)
        {
            List<string> tableNames = new List<string> { "Applications", "Containers", "Records", "Notifications" };

            foreach (string tableName in tableNames)
            {
                // Table name can't be parameterized, so it's directly interpolated safely.
                string query = $"SELECT name FROM `{tableName}` WHERE name = @Name";

                using (MySqlCommand cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Name", name);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return false; // Name exists in the current table
                        }
                    }
                }
            }

            return true; // Name is unique across all tables
        }

        // This method is used to select the application id from the database
        private int SelectApplication(string application, MySqlConnection conn)
        {
            var app_id = 0;
                using (var cmd = new MySqlCommand("SELECT id FROM Applications WHERE name = @Application", conn))
                {
                    cmd.Parameters.AddWithValue("@Application", application);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            app_id = Int32.Parse(reader["id"].ToString());
                        }
                        else
                        {
                            app_id = -1;
                        }
                    }
                }
            return app_id;
        }

        // This method is used to select the container id from the database
        private int SelectContainer(string container, int app_id, MySqlConnection conn)
        {
            var cont_id = 0;

            using (var cmd2 = new MySqlCommand("SELECT id FROM Containers WHERE name = @Container AND parent = @Cont_id", conn))
            {
                cmd2.Parameters.AddWithValue("@Container", container);
                cmd2.Parameters.AddWithValue("@Cont_id", app_id);
                using (var reader2 = cmd2.ExecuteReader())
                {
                    if (reader2.Read())
                    {
                        cont_id = Int32.Parse(reader2["id"].ToString());
                    }
                    else
                    {
                        cont_id = -1;
                    }
                }
            }
            return cont_id;
        }


        // This method is used to send messages to the endpoints on the notifications
        private void SendMessages(string content, string container, int cont_id, string record, int ev, MySqlConnection conn)
        {
            List<String> endpoints = new List<String>();

            using (var cmd = new MySqlCommand("SELECT endpoint FROM Notifications WHERE event IN (@ev, 2) AND parent = @parent AND enabled = 1", conn))
            {
                cmd.Parameters.AddWithValue("@ev", ev);
                cmd.Parameters.AddWithValue("@parent", cont_id);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        endpoints.Add(reader["endpoint"].ToString());
                    }
                }
            }

            foreach (var endpoint in endpoints)
            {
                if (endpoint == null)
                {
                    continue;
                }
                try
                {
                    // cria uma estrutura xml uniformizada para os creates e deletes

                    XmlDocument doc = new XmlDocument();

                    XmlElement contentElement = doc.CreateElement("content");
                    contentElement.InnerText = ev == 0 ? content : record;
                    XmlElement root = doc.CreateElement("SomiodMessage");

                    XmlElement operation = doc.CreateElement("operation");
                    operation.InnerText = ev == 0 ? "Created" : "Deleted";

                    root.AppendChild(operation);
                    root.AppendChild(contentElement);

                    string xml = root.OuterXml;

                    if (endpoint.Contains("http://") || endpoint.Contains("https://"))
                    {
                        using (RestClient client = new RestClient(endpoint))
                        {
                            RestRequest request = new RestRequest("", Method.Post);
                            request.RequestFormat = DataFormat.Xml;

                            request.AddObject(xml);

                            client.Execute(request);
                            // Se o post não funcionar a culpa é do utilizador
                            // por isso não me importo com a resposta
                        }
                    }
                    else if (endpoint.Contains("mqtt://"))
                    {
                        var newEnd = endpoint.Replace("mqtt://", "");
                        MqttClient mClient = new MqttClient(newEnd);

                        mClient.Connect(Guid.NewGuid().ToString());
                        mClient.Publish(container, System.Text.Encoding.UTF8.GetBytes(xml));
                        // to prevent an error that was persisting
                        Thread.Sleep(100);
                    }
                }
                catch (Exception)
                {
                    continue;
                }
            }

        }



        /**
         * GETS
        */


        [Route("api/somiod")]
        public IHttpActionResult GetApplications(HttpRequestMessage requestHeader)
        {
            

            List<Application> applications = new List<Application>();


            // create a connection to the mysql database
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    //locate feature
                    if (requestHeader.Headers.Contains("somiod-locate"))
                    {
                        //locate apps
                        if (requestHeader.Headers.GetValues("somiod-locate").Contains("application"))
                        {
                            var names = new List<String>();
                            using (MySqlCommand cmd = new MySqlCommand("Select name from Applications", conn))
                            {
                                using (MySqlDataReader reader = cmd.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        names.Add(reader["name"].ToString());
                                    }
                                }
                                return Ok(names);
                            }
                        }
                        //locate containers
                        else if (requestHeader.Headers.GetValues("somiod-locate").Contains("container"))
                        {
                            var names = new List<String>();
                            using (MySqlCommand cmd = new MySqlCommand("Select name from Containers", conn))
                            {
                                using (MySqlDataReader reader = cmd.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        names.Add(reader["name"].ToString());
                                    }
                                }
                                return Ok(names);
                            }
                        }
                        //locate records
                        else if (requestHeader.Headers.GetValues("somiod-locate").Contains("record"))
                        {
                            var names = new List<String>();
                            using (MySqlCommand cmd = new MySqlCommand("Select name from Records", conn))
                            {
                                using (MySqlDataReader reader = cmd.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        names.Add(reader["name"].ToString());
                                    }
                                }
                                return Ok(names);
                            }
                        }
                        // locate notifications
                        else if (requestHeader.Headers.GetValues("somiod-locate").Contains("notification"))
                        {
                            var names = new List<String>();
                            using (MySqlCommand cmd = new MySqlCommand("Select name from Notifications", conn))
                            {
                                using (MySqlDataReader reader = cmd.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        names.Add(reader["name"].ToString());
                                    }
                                }
                                return Ok(names);
                            }
                        }
                        else
                        {
                            return BadRequest();
                        }
                    }
                    //get all applications the normal way
                    else
                    {
                        using (MySqlCommand cmd = new MySqlCommand("SELECT * FROM Applications", conn))
                        {
                            using (MySqlDataReader reader = cmd.ExecuteReader())
                            {

                                while (reader.Read())
                                {
                                    var application = new Application
                                    {
                                        id = Int32.Parse(reader["id"].ToString()),
                                        name = reader["name"].ToString(),
                                        creation_datetime = DateTime.TryParse(reader["creation_datetime"].ToString(), out DateTime parsedDate)
                                        ? parsedDate
                                        : DateTime.MinValue
                                    };

                                    applications.Add(application);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                return InternalServerError();
            }



            return Ok(applications);
        }



        [HttpGet]
        [Route("api/somiod/{application}")]
        public IHttpActionResult GetApplication(String application, HttpRequestMessage requestHeader)
        {
           
            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                //locate feature
                if (requestHeader.Headers.Contains("somiod-locate"))
                {
                    var app_id = SelectApplication(application, conn);

                    if (app_id == -1)
                    {
                        return NotFound();
                    }
                    //locate containers in the app
                    if (requestHeader.Headers.GetValues("somiod-locate").Contains("container"))
                    {
                        var names = new List<String>();
                        using (var cmd = new MySqlCommand("Select name from Containers where parent in (select id from Applications where name = @Name)", conn))
                        {
                            cmd.Parameters.AddWithValue("@Name", application);
                            using (var reader = cmd.ExecuteReader())
                            {

                                while (reader.Read())
                                {
                                    names.Add(reader["name"].ToString());
                                }
                            }
                            return Ok(names);
                        }
                    }
                    //locate records in the app
                    else if (requestHeader.Headers.GetValues("somiod-locate").Contains("record"))
                    {
                        var records = new List<String>();
                        using (var cmd = new MySqlCommand("Select name from Records where parent in (select id from Containers where parent in (select id from Applications where name = @Name))", conn))
                        {
                            cmd.Parameters.AddWithValue("@Name", application);
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    records.Add(reader["name"].ToString());
                                }
                            }
                            return Ok(records);
                        }
                    }
                    //locate notifications in the app
                    else if (requestHeader.Headers.GetValues("somiod-locate").Contains("notification"))
                    {
                        var notifications = new List<String>();
                        using (var cmd = new MySqlCommand("Select name from Notifications where parent in (select id from Containers where parent in (select id from Applications where name = @Name))", conn))
                        {
                            cmd.Parameters.AddWithValue("@Name", application);
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    notifications.Add(reader["name"].ToString());
                                }
                            }
                            return Ok(notifications);
                        }
                    }
                    else
                    {
                        return BadRequest();
                    }
                }
                //get the application the normal way
                using (var cmd = new MySqlCommand("SELECT * FROM Applications WHERE name = @Name", conn))
                {
                    cmd.Parameters.AddWithValue("@Name", application);
                    try
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var app = new Application
                                {
                                    id = Int32.Parse(reader["id"].ToString()),
                                    name = reader["name"].ToString(),
                                    creation_datetime = DateTime.TryParse(reader["creation_datetime"].ToString(), out DateTime parsedDate)
                                        ? parsedDate
                                        : DateTime.MinValue
                                };
                                return Ok(app);
                            }
                            return NotFound();
                        }
                    }
                    catch (Exception)
                    {
                        return InternalServerError();
                    }

                }
            }
        }



        [HttpGet]
        [Route("api/somiod/{application}/{container}")]
        public IHttpActionResult GetContainer(String application, String container, HttpRequestMessage request)
        {
           
           
            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                //locate feature
                if (request.Headers.Contains("somiod-locate"))
                {
                    //locate records in the container
                    if (request.Headers.GetValues("somiod-locate").Contains("record"))
                    {
                        var names = new List<String>();

                        using (var cmd = new MySqlCommand("SELECT name FROM Records WHERE parent in (Select id from Containers Where name = @Container)", conn))
                        {
                            cmd.Parameters.AddWithValue("@Container", container);
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    names.Add(reader.GetString(0));
                                }
                                return Ok(names);
                            }
                        }

                    }
                    //locate notifications in the container
                    else if (request.Headers.GetValues("somiod-locate").Contains("notification"))
                    {
                        var names = new List<String>();
                        using (var cmd = new MySqlCommand("SELECT name FROM Notifications WHERE parent in (Select id from Containers Where name = @Container)", conn))
                        {
                            cmd.Parameters.AddWithValue("@Container", container);
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    names.Add(reader.GetString(0));
                                }
                                return Ok(names);
                            }
                        }
                    }
                    else
                    {
                        //when the somiod-locate has a value that is not supported
                        return BadRequest();
                    }
                }
                // get the container the normal way

                var app_id =  SelectApplication(application, conn);

                if (app_id == -1)
                {
                    return NotFound();
                }

                using (var cmd2 = new MySqlCommand("SELECT * FROM Containers WHERE name = @Container AND parent = @App_id", conn))
                {
                    cmd2.Parameters.AddWithValue("@Container", container);
                    cmd2.Parameters.AddWithValue("@App_id", app_id);
                    using (var reader2 = cmd2.ExecuteReader())
                    {
                        if (reader2.Read())
                        {
                            Container cont = new Container
                            {
                                id = Int32.Parse(reader2["id"].ToString()),
                                name = reader2["name"].ToString(),
                                creation_datetime = DateTime.TryParse(reader2["creation_datetime"].ToString(), out DateTime parsedDate)
                                         ? parsedDate
                                         : DateTime.MinValue,
                                parent = Int32.Parse(reader2["parent"].ToString())
                            };
                            return Ok(cont);
                        }
                        else
                        {
                            return NotFound();
                        }
                    }
                }
            }
        }


        [HttpGet]
        [Route("api/somiod/{application}/{container}/record/{record}")]
        public IHttpActionResult GetRecord(String application, String container, String record, HttpRequestMessage request)
        {
            

            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();

                var app_id = SelectApplication(application, conn);

                if (app_id == -1)
                {
                    return NotFound();
                }

                var cont_id = SelectContainer(container, app_id, conn);

                if (cont_id == -1)
                {
                    return NotFound();
                }

                using (var cmd3 = new MySqlCommand("SELECT  * FROM Records WHERE parent = @Cont_id", conn))
                {
                    cmd3.Parameters.AddWithValue("@Cont_id", cont_id);
                    using (var reader3 = cmd3.ExecuteReader())
                    {
                        if (reader3.Read())
                        {
                            Record rec = new Record
                            {
                                id = Int32.Parse(reader3["id"].ToString()),
                                name = reader3["name"].ToString(),
                                creation_datetime = DateTime.TryParse(reader3["creation_datetime"].ToString(), out DateTime parsedDate)
                                         ? parsedDate
                                         : DateTime.MinValue,
                                parent = Int32.Parse(reader3["parent"].ToString()),
                                content = reader3["content"].ToString(),
                            };
                            return Ok(rec);
                        }
                        else
                        {
                            return NotFound();
                        }
                    }
                }
            }
        }

        [HttpGet]
        [Route("api/somiod/{application}/{container}/notification/{notification}")]
        public IHttpActionResult GetNotification(String application, String container, String notification, HttpRequestMessage request)
        {
           

            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                var app_id = SelectApplication(application, conn);

                if (app_id == -1)
                {
                    return NotFound();
                }

                var cont_id = SelectContainer(container, app_id, conn);

                if (cont_id == -1)
                {
                    return NotFound();
                }

                using (var cmd3 = new MySqlCommand("SELECT * FROM Notifications WHERE parent = @Cont_id", conn))
                {
                    cmd3.Parameters.AddWithValue("@Cont_id", cont_id);
                    using (var reader3 = cmd3.ExecuteReader())
                    {
                        if (reader3.Read())
                        {
                            Notification not = new Notification
                            {
                                id = Int32.Parse(reader3["id"].ToString()),
                                name = reader3["name"].ToString(),
                                creation_datetime = DateTime.TryParse(reader3["creation_datetime"].ToString(), out DateTime parsedDate)
                                         ? parsedDate
                                         : DateTime.MinValue,
                                parent = Int32.Parse(reader3["parent"].ToString()),
                                @event = Int32.Parse(reader3["event"].ToString()),
                                endpoint = reader3["endpoint"].ToString(),
                                enabled = reader3["enabled"].ToString() == "1"
                            };
                            return Ok(not);
                        }
                        else
                        {
                            return NotFound();
                        }
                    }
                }
            }
        }



        /**
         * POSTS
        */


        [HttpPost]
        [Route("api/somiod")]
        public IHttpActionResult PostApplication([FromBody] Application application, HttpRequestMessage request)
        {
            if (request.Headers.Contains("res-type"))
            {
                if (!request.Headers.GetValues("res-type").Contains("application"))
                {
                    return BadRequest();
                }
            }
            else
            {
                return BadRequest();
            }

            //when the body is empty or has unknown values
            if (application == null)
            {
                return BadRequest();
            }
            // create a connection to the mysql database
            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                try
                {
                    conn.Open();
                }
                catch (Exception)
                {
                    return InternalServerError();
                }

                // check if the name is unique
                if (!checkUniqueName(application.name, conn))
                {
                    return BadRequest();
                }
                using (MySqlCommand cmd = new MySqlCommand("INSERT INTO Applications (name, creation_datetime) VALUES (@Name, @CreationDateTime)", conn))
                {
                    try
                    {
                        cmd.Parameters.AddWithValue("@Name", application.name);
                        cmd.Parameters.AddWithValue("@CreationDateTime", DateTime.Now);
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception)
                    {
                        return InternalServerError();
                    }
                }
            }

            return Ok();
        }


        [HttpPost]
        [Route("api/somiod/{application}")]
        public IHttpActionResult PostContainer(String application, [FromBody] Container container, HttpRequestMessage request)
        {
            if (request.Headers.Contains("res-type"))
            {
                if (!request.Headers.GetValues("res-type").Contains("container"))
                {
                    return BadRequest();
                }
            }
            else
            {
                return BadRequest();
            }
            if (container == null)
            {
                return BadRequest();
            }

            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                if (!checkUniqueName(container.name, conn))
                {
                    return BadRequest();
                }
                var app_id = SelectApplication(application, conn);

                if (app_id == -1)
                {
                    return NotFound();
                }

                using (var cmd2 = new MySqlCommand("INSERT INTO Containers (name, creation_datetime, parent) VALUES (@Name, @CreationDateTime, @Parent)", conn))
                {
                    try
                    {
                        cmd2.Parameters.AddWithValue("@Name", container.name);
                        cmd2.Parameters.AddWithValue("@CreationDateTime", DateTime.Now);
                        cmd2.Parameters.AddWithValue("@Parent", app_id);
                        cmd2.ExecuteNonQuery();
                        return Ok();
                    }
                    catch (Exception)
                    {

                        return InternalServerError();
                    }
                }
            }
        }

        [HttpPost]
        [Route("api/somiod/{application}/{container}/record")]
        public IHttpActionResult PostRecord(String application, String container, [FromBody] Record record, HttpRequestMessage request)
        {
            if (request.Headers.Contains("res-type"))
            {
                if (!request.Headers.GetValues("res-type").Contains("record"))
                {
                    return BadRequest();
                }
            }
            else
            {
                return BadRequest();
            }

            if (record == null)
            {
                return BadRequest();
            }

            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                if (!checkUniqueName(record.name, conn))
                {
                    return BadRequest();
                }

                var app_id = SelectApplication(application, conn);

                if (app_id == -1)
                {
                    return NotFound();
                }

                var cont_id = SelectContainer(container, app_id, conn);

                if (cont_id == -1)
                {
                    return NotFound();
                }

                using (var cmd3 = new MySqlCommand("INSERT INTO Records (name, creation_datetime, parent, content) VALUES (@Name, @CreationDateTime, @Parent, @Content)", conn))
                {
                    try
                    {
                        cmd3.Parameters.AddWithValue("@Name", record.name);
                        cmd3.Parameters.AddWithValue("@CreationDateTime", DateTime.Now);
                        cmd3.Parameters.AddWithValue("@Parent", cont_id);
                        cmd3.Parameters.AddWithValue("@Content", record.content);
                        cmd3.ExecuteNonQuery();
                    }
                    catch (Exception)
                    {
                        return InternalServerError();
                    }
                }

                // event 0 -> created
                SendMessages(record.content, container, cont_id, record.name, 0, conn);

                return Ok();
            }
        }

        [HttpPost]
        [Route("api/somiod/{application}/{container}/notification")]
        public IHttpActionResult PostNotification(String application, String container, [FromBody] Notification notification, HttpRequestMessage request)
        {

            if (request.Headers.Contains("res-type"))
            {
                if (!request.Headers.GetValues("res-type").Contains("notification"))
                {
                    return BadRequest();
                }
            }
            else
            {
                return BadRequest();
            }
            if (notification == null)
            {
                return BadRequest();
            }

            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                if (!checkUniqueName(notification.name, conn))
                {
                    return BadRequest();
                }

                var app_id = SelectApplication(application, conn);

                if (app_id == -1)
                {
                    return NotFound();
                }

                var cont_id = SelectContainer(container, app_id, conn);

                if (cont_id == -1)
                {
                    return NotFound();
                }

                using (var cmd3 = new MySqlCommand("INSERT INTO Notifications (name, creation_datetime, parent, event, endpoint, enabled) VALUES (@Name, @CreationDateTime, @Parent, @Event, @Endpoint, @Enabled)", conn))
                {
                    try
                    {
                        cmd3.Parameters.AddWithValue("@Name", notification.name);
                        cmd3.Parameters.AddWithValue("@CreationDateTime", DateTime.Now);
                        cmd3.Parameters.AddWithValue("@Parent", cont_id);
                        cmd3.Parameters.AddWithValue("@Event", notification.@event);
                        cmd3.Parameters.AddWithValue("@Endpoint", notification.endpoint);
                        cmd3.Parameters.AddWithValue("@Enabled", notification.enabled);
                        cmd3.ExecuteNonQuery();
                        return Ok();
                    }
                    catch (Exception)
                    {
                        return InternalServerError();
                    }

                }
            }
        }

        /**
         * PUTS
        */

        [HttpPut]
        [Route("api/somiod/{name}")]
        public IHttpActionResult PutApplication(string name, [FromBody] Application application, HttpRequestMessage request)
        {
            if (request.Headers.Contains("res-type"))
            {
                if (!request.Headers.GetValues("res-type").Contains("application"))
                {
                    return BadRequest();
                }
            }
            else
            {
                return BadRequest();
            }

            // when the body is empty or has unknown values
            if (application == null)
            {
                return BadRequest();
            }

            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                if (!checkUniqueName(application.name, conn))
                {
                    return BadRequest();
                }
                using (MySqlCommand cmd = new MySqlCommand("UPDATE Applications SET name = @NameNew WHERE name = @NameOld", conn))
                {
                    cmd.Parameters.AddWithValue("@NameOld", name);
                    cmd.Parameters.AddWithValue("@NameNew", application.name);
                    try
                    {
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception)
                    {
                        return InternalServerError();
                    }
                    return Ok();
                }
            }
        }

        [HttpPut]
        [Route("api/somiod/{application}/{name}")]
        public IHttpActionResult PutContainer(string application, string name, [FromBody] Container container, HttpRequestMessage request)
        {
            if (request.Headers.Contains("res-type"))
            {
                if (!request.Headers.GetValues("res-type").Contains("container"))
                {
                    return BadRequest();
                }
            }
            else
            {
                return BadRequest();
            }

            // when the body is empty or has unknown values
            if (container == null)
            {
                return BadRequest();
            }

            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                if (!checkUniqueName(container.name, conn))
                {
                    return BadRequest();
                }
                var app_id = SelectApplication(application, conn);

                if (app_id == -1)
                {
                    return NotFound();
                }

                using (MySqlCommand cmd = new MySqlCommand("UPDATE Containers SET name = @NameNew WHERE name = @NameOld AND parent = @Parent", conn))
                {
                    cmd.Parameters.AddWithValue("@NameOld", name);
                    cmd.Parameters.AddWithValue("@NameNew", container.name);
                    cmd.Parameters.AddWithValue("@Parent", app_id);
                    try
                    {
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception)
                    {
                        return InternalServerError();
                    }
                    return Ok();
                }
            }
        }

        /**
         * DELETES
        */

        [HttpDelete]
        [Route("api/somiod/{name}")]
        public IHttpActionResult DeleteApplication(string name, HttpRequestMessage request)
        {
           
            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand("DELETE FROM Applications WHERE name = @Name", conn))
                {
                    cmd.Parameters.AddWithValue("@Name", name);
                    try
                    {
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception)
                    {
                        return InternalServerError();
                    }
                    return Ok();
                }
            }
        }

        [HttpDelete]
        [Route("api/somiod/{application}/{name}")]
        public IHttpActionResult DeleteContainer(string application, string name, HttpRequestMessage request)
        {
            

            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                var app_id = SelectApplication(application, conn);

                if (app_id == -1)
                {
                    return NotFound();
                }

                using (MySqlCommand cmd = new MySqlCommand("DELETE FROM Containers WHERE name = @Name AND parent = @Parent", conn))
                {
                    cmd.Parameters.AddWithValue("@Name", name);
                    cmd.Parameters.AddWithValue("@Parent", app_id);
                    try
                    {
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception)
                    {
                        return InternalServerError();
                    }
                    return Ok();
                }
            }
        }

        [HttpDelete]
        [Route("api/somiod/{application}/{container}/record/{record}")]
        public IHttpActionResult DeleteRecord(String application, String container, String record, HttpRequestMessage request)
        {
            

            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                var app_id = SelectApplication(application, conn);

                if (app_id == -1)
                {
                    return NotFound();
                }

                var cont_id = SelectContainer(container, app_id, conn);

                if (cont_id == -1)
                {
                    return NotFound();
                }

                using (var cmd = new MySqlCommand("DELETE FROM Records WHERE name = @Name AND parent = @Parent", conn))
                {
                    cmd.Parameters.AddWithValue("@Name", record);
                    cmd.Parameters.AddWithValue("@Parent", cont_id);
                    try
                    {
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception)
                    {
                        return InternalServerError();
                    }
                }

                // eveto 1 -> deleted
                SendMessages(record, container, cont_id, record, 1, conn);
                return Ok();
            }
        }

        [HttpDelete]
        [Route("api/somiod/{application}/{container}/notification/{notification}")]
        public IHttpActionResult DeleteNotification(String application, String container, String notification, HttpRequestMessage request)
        {
           

            using (var conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                var app_id = SelectApplication(application, conn);
                if (app_id == -1)
                {
                    return NotFound();
                }
                var cont_id = SelectContainer(container, app_id, conn);
                if (cont_id == -1)
                {
                    return NotFound();
                }
                using (var cmd = new MySqlCommand("DELETE FROM Notifications WHERE name = @Name AND parent = @Parent", conn))
                {
                    cmd.Parameters.AddWithValue("@Name", notification);
                    cmd.Parameters.AddWithValue("@Parent", cont_id);
                    try
                    {
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception)
                    {
                        return InternalServerError();
                    }
                    return Ok();
                }
            }
        }
    }
}