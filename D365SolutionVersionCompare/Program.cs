// See https://aka.ms/new-console-template for more information
using Microsoft.Xrm.Sdk.Messages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

Console.WriteLine("Tool Starting");

// Read the JSON config file into a Config object
var jsonString = File.ReadAllText("appsettings.json");
var config = JsonConvert.DeserializeObject<Config>(jsonString);
var solutionsDict = new Dictionary<string, Dictionary<string, string>>();

// Access the URLs in the Config object's OrgUrls property and run the query to get Solutions
foreach (var orgUrl in config.OrgUrls)
{
    Console.WriteLine($" Running for org url: {orgUrl.URL}");

    var connString = String.Format(config.ConnectionString, orgUrl.URL);
    string responseContent = String.Empty;

    try
    {        
        // Create a Dataverse service client using the default connection string.
        Microsoft.PowerPlatform.Dataverse.Client.ServiceClient dataverseClient = new(connString);

        var response = dataverseClient.ExecuteWebRequest(HttpMethod.Get, "solutions?$select=solutionid,uniquename,friendlyname,installedon,ismanaged,version&$expand=publisherid($select=friendlyname)&$filter=ismanaged%20eq%20true", "", null);

        if (response.IsSuccessStatusCode)
        {

            responseContent = await response.Content.ReadAsStringAsync();
        }
        else
        {
            Console.WriteLine($"Query operation failed for {orgUrl.URL}:\nReason: {response.ReasonPhrase}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Query operation failed for {orgUrl.URL}:\nMessage: {ex.Message}\nDetail: {ex.InnerException}");
        break;
    }

    JArray solutions= new JArray();
    try
    {
        JObject responseObject = JObject.Parse(responseContent);
        solutions = (JArray)responseObject["value"];

    }
    catch(Exception ex)
    {
        Console.WriteLine($"Eror parsing solution response {orgUrl.URL}:\nMessage: {ex.Message}\nDetail: {ex.InnerException}");
        break;
    }

    foreach (var solution in solutions)
    {
        var uniqueName = solution.Value<string>("uniquename");
        if (!solutionsDict.TryGetValue(uniqueName, out var solutionVersions))
        {
            solutionVersions = new Dictionary<string, string>();
            solutionsDict[uniqueName] = solutionVersions;
        }
        var version = solution.Value<string>("version");
        if (!string.IsNullOrEmpty(version))
        {
            solutionVersions[orgUrl.URL] = version;
        }
    }

}

try
{
    using (var csvWriter = new StreamWriter("SolutionCompareOutput.csv"))
    {
        StringBuilder header = new StringBuilder();
        foreach (var url in config.OrgUrls)
        {
            header.Append(url.URL);
            header.Append(",");
        }

        csvWriter.WriteLine("Solution," + header.ToString());

        foreach (var kvp in solutionsDict)
        {
            var uniqueName = kvp.Key;
            var solutionVersions = kvp.Value;

            var versions = new List<string>();
            foreach (var url in config.OrgUrls)
            {
                versions.Add(solutionVersions.TryGetValue(url.URL, out var version) ? version : "");
            }
            csvWriter.WriteLine($"{uniqueName}," + string.Join(",", versions));
        }
    }
}
catch(Exception ex)
{
    Console.WriteLine($"Failed Building or Writing CSV Output:\nMessage: {ex.Message}\nDetail: {ex.InnerException}");
}

Console.WriteLine("Done");

# region appsettings classes
public class Config
{
    public List<OrgUrl>? OrgUrls { get; set; }
    public string? ConnectionString { get; set; }
}

public class OrgUrl
{
    public string? URL { get; set; }
}
#endregion 