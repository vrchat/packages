using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Utilities;
using YamlDotNet.Serialization;

class Build : NukeBuild
{
    public static int Main () => Execute<Build>(x => x.CreateAndSaveNewIndex);

    [Parameter("Absolute path to list YML file")]
    private readonly AbsolutePath SourceListPath = RootDirectory.Parent / "list.yml";
    
    [Parameter("Absolute path to list YML file")]
    private readonly AbsolutePath RepoListPath = RootDirectory.Parent / "docs" / "index.json";

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    Target CreateAndSaveNewIndex => _ => _
        .Executes(async () =>
        {
            var repoList = await MakeVRCRepoListFromYaml(SourceListPath);
            if (repoList == null)
            {
                Serilog.Log.Error($"Could not create VRCRepoList");
                return;
            }
            File.WriteAllText(RepoListPath, repoList.ToString());
            Serilog.Log.Information($"Saved updated index to {RepoListPath}");
        });

    private async Task<JObject> MakeVRCRepoListFromYaml(AbsolutePath path)
    {
        var listing = RepoListYaml.FromPath(path);
            Serilog.Log.Information($"Deserialized and found {listing.packages.Length} packages.");

            var tasks = new List<Task<string>>();
            JObject jRepoList = new JObject();
            jRepoList["name"] = listing.name;
            jRepoList["author"] = listing.author;
            jRepoList["packages"] = new JObject();
            JObject jPackages = jRepoList["packages"] as JObject;

            // Go through each package and resolve it from its url to json and we'll have a listing of Json info
            try
            {
                using (var client = new HttpClient())
                {
                    foreach (string url in listing.packages)
                    {
                        tasks.Add(client.GetStringAsync(url));
                    }

                    while(tasks.Count > 0) {
                        var task = await Task.WhenAny(tasks);
                        tasks.Remove(task);
                        try
                        {
                            var jsonString = await task;
                            try
                            {
                                var j = JObject.Parse(task.Result);
                                var name = j["name"]?.ToString();
                                var version = j["version"]?.ToString();
                                if (name.IsNullOrWhiteSpace() || version.IsNullOrWhiteSpace())
                                {
                                    Serilog.Log.Error($"Package is missing either name or version: {jsonString}");
                                    throw new Exception();
                                }

                                if (!jPackages.ContainsKey(name))
                                {
                                    jPackages[name] = new JObject();
                                    jPackages[name]["versions"] = new JObject();
                                }
                                jPackages[name]["versions"][version] = JObject.Parse(jsonString);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                                throw;
                            }
                        }
                        catch (OperationCanceledException e)
                        {
                            Serilog.Log.Error($"Retrieving packages cancelled: {e.Message}");
                            return null;
                        }
                        catch (Exception e)
                        {
                            Serilog.Log.Error($"Couldn't resolve a package: {e.Message}");
                            return null;
                        }
                    }
                    
                    await Task.WhenAll(tasks);
                    return jRepoList;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
    }
}

[Serializable]
public class RepoListYaml
{
    public string name;
    public string author;
    public string[] packages;

    public static RepoListYaml FromPath(AbsolutePath path)
    {
        Assert.FileExists(path);
            
        var text = File.ReadAllText(path);
        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build();
        return deserializer.Deserialize<RepoListYaml>(text);
    }
}