using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Training;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Training.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace CustomVisionObjectDetection
{
    class Program
    {
        static void Main(string[] args)
        {
            // Add your training key from the settings page of the portal
            string trainingKey = "YOUR TRAINING KEY";

            // Create the Api, passing in the training key
            TrainingApi trainingApi = new TrainingApi() { ApiKey = trainingKey };

            // Find the object detection domain
            var domains = trainingApi.GetDomains();
            var objDetectionDomain = domains.FirstOrDefault(d => d.Type == "ObjectDetection");

            // Create a new project
            Console.WriteLine("Creating new project:");
            var project =  trainingApi.CreateProject("CSharp Office", null, objDetectionDomain.Id);

            //var project = trainingApi.GetProject();

            using (StreamReader r = new StreamReader("Office.json"))
            {
                string json = r.ReadToEnd();
                Console.Write(json);
                var data = JObject.Parse(json);

                //Export Tags from code
                var inputTags = data["inputTags"].ToString().Split(',');
                foreach (string t in inputTags)
                {
                    Console.WriteLine(t);
                    trainingApi.CreateTag(project.Id, t);
                }


                // Get all tagsIDs created on custom vision and add into a dictionary
                Dictionary<string, Guid> imgtags = new Dictionary<string, Guid>();
                foreach (Tag t in trainingApi.GetTags(project.Id))
                {
                    Console.WriteLine(t.Name +" - "+ t.Id);
                    imgtags.Add(t.Name, t.Id);
                }

                // Create Image TagIDs with normalized points
                Dictionary<string, double[]> imgtagdic = new Dictionary<string, double[]>();
                foreach (var a in data["visitedFrames"])
                {
                    Console.WriteLine(a);
                    try {
                        foreach (var key in data["frames"][a.ToString()])
                        {
                            double x1 = Convert.ToDouble(key["x1"].ToString());
                            double y1 = Convert.ToDouble(key["y1"].ToString());
                            double x2 = Convert.ToDouble(key["x2"].ToString());
                            double y2 = Convert.ToDouble(key["y2"].ToString());
                            int h = Convert.ToInt32(key["height"].ToString());
                            int w = Convert.ToInt32(key["width"].ToString());
                            double tleft = (double)x1 / (double)w;
                            double ttop = (double)y1 / (double)h;
                            double twidth = (double)(x2 - x1) / (double)w;
                            double theight = (double)(y2 - y1) / (double)h;
                            try {
                                string tag = key["tags"][0].ToString();
                                // Defining UniqueID per tags in photo below  
                                imgtagdic.Add(imgtags[tag].ToString() + ","+ a.ToString() + ',' + key["name"].ToString() + tag, new double[] { tleft, ttop, twidth, theight });
                            }
                            catch {
                                Console.WriteLine("An Error occured on imtagdic");
                            }
                        }
                    }
                    catch {
                        Console.WriteLine("An Error occured on json parsing");
                    }
                }

                // Add all images for fork
                var imagePath = Path.Combine("", "Office");
                string[] allphotos = Directory.GetFiles(imagePath);


                var imageFileEntries = new List<ImageFileCreateEntry>();
                foreach (var key in imgtagdic)
                {
                    Guid tagguid = Guid.Parse(key.Key.Split(',')[0]);
                    var fileName = allphotos[Convert.ToInt32(key.Key.Split(',')[1])];
                    imageFileEntries.Add(new ImageFileCreateEntry(fileName, File.ReadAllBytes(fileName), null, new List<Region>(new Region[] { new Region(tagguid, key.Value[0], key.Value[1], key.Value[2], key.Value[3]) })));

                    //
                    //Tried the add list of tags 
                    //List<Guid> listtags = new List<Guid> { tagguid };
                    //imageFileEntries.Add(new ImageFileCreateEntry(fileName, File.ReadAllBytes(fileName), listtags, new List<Region>(new Region[] { new Region(tagguid, key.Value[0], key.Value[1], key.Value[2], key.Value[3]) })));
                }

                Console.WriteLine("\tUpload has started!");
                trainingApi.CreateImagesFromFiles(project.Id, new ImageFileCreateBatch(imageFileEntries));
                Console.WriteLine("\tUpload is done!");

                // Now there are images with tags start training the project
                Console.WriteLine("\tTraining");
                var iteration = trainingApi.TrainProject(project.Id);

                // The returned iteration will be in progress, and can be queried periodically to see when it has completed
                while (iteration.Status != "Completed")
                {
                    Thread.Sleep(1000);

                    // Re-query the iteration to get its updated status
                    iteration = trainingApi.GetIteration(project.Id, iteration.Id);
                }

                // The iteration is now trained. Make it the default project endpoint
                iteration.IsDefault = true;
                trainingApi.UpdateIteration(project.Id, iteration.Id, iteration);
                Console.WriteLine("Done!\n");

            }




        }


    }
}
