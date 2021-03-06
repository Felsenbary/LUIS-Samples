/*-----------------------------------------------------------------------------
This example demonstrates how to collect ApplicationInsights data for LUIS endpoint
queries from an Azure Function. 
For a complete walkthrough of creating this example, see the article at
https://aka.ms/<URL COMING SOON>
-----------------------------------------------------------------------------*/
#r "Newtonsoft.Json"

using System;
using System.Net;
using System.Text;
using System.Diagnostics;

// application insights
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

using Newtonsoft.Json;

// Azure & LUIS Information
const string LUISappID = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx";
const string LUISsubscriptionKey = "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
const string BingSpellCheckKey = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx";

// HTTP Request to LUIS
static HttpClient httpClient = new HttpClient();

// Application Insights
private static TelemetryClient telemetryClient = new TelemetryClient();
private static string key = TelemetryConfiguration.Active.InstrumentationKey = System.Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY", EnvironmentVariableTarget.Process);

// Function Name for Application Insights Dependency Track
const string fnName = "LUIS-example-fn";

public static class LUIS
{
    public class luis_intent
    {
        public string intent { get; set; }
        public float score { get; set; }
    }
    public class luis_entity
    {
        public string entity { get; set; }
        public string type { get; set; }
        public int startIndex { get; set; }
        public int endIndex { get; set; }
        public float score { get; set; }
    }
    public class LUISPrediction
    {
        public string LuisAppID { get; set; }
        public string LuisSubscriptionKey { get; set; }
        public string BingSpellCheckKey { get; set; }
        public string Query { get; set; }
        public string alteredQuery { get; set; }
        public string region { get; set; }
        public string success { get; set; }
        public string errorMessage { get; set; }

        public luis_intent topScoringIntent { get; set; }
        public List<luis_intent> intents;
        public List<luis_entity> entities;
    }

    public static async Task<HttpResponseMessage> EndpointQuery(HttpRequestMessage req, TraceWriter log, string query, string region)
    {
        // default to westus region
        string endpointRegion = region ?? "westus";

        // LUIS endpoint URL with query
        string LUISendpoint = "https://" + endpointRegion + ".api.cognitive.microsoft.com/luis/v2.0/apps/" + LUISappID + "/?verbose=true&q=" + query;

        // If BING SPELL CHECK, add to querystring
        if (!String.IsNullOrEmpty(BingSpellCheckKey)) LUISendpoint += "&spellCheck=true&bing-spell-check-subscription-key=" + BingSpellCheckKey;

        // Add HTTP Authentication Header
        httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Keyy", LUISsubscriptionKey);

        // Create Application Insights Dependency
        var dependencyTelemetry = new DependencyTelemetry
        {
            Type = "REST",
            Name = "LUIS-" + endpointRegion
        };

        // Application Insights Dependency start timer
        dependencyTelemetry.Start();

        // Application Insights Dependency HTTP URL
        dependencyTelemetry.Data = LUISendpoint;

        // Request HTTP endpoint
        var LUIS_response = await httpClient.GetAsync(LUISendpoint);

        // Stop Application Insights Dependency timer 
        dependencyTelemetry.Stop();

        // handle 4xxx
        if (!LUIS_response.IsSuccessStatusCode)
        {
            var errorResults = new LUISPrediction();

            errorResults.success = "false";
            errorResults.errorMessage = LUIS_response.ReasonPhrase;
            errorResults.Query = query;
            errorResults.region = endpointRegion;
            errorResults.LuisAppID = LUISappID;
            errorResults.LuisSubscriptionKey = LUISsubscriptionKey;

            // send errorResults to ApplicationInsights
            LUIS.ApplicationInsightsTraceError(dependencyTelemetry, errorResults);

            return LUIS_response;
        }

        // get LUIS response content as LUISPrediction Class
        var queryResults = await LUIS_response.Content.ReadAsAsync<LUISPrediction>();

        // add request details to queryResults
        queryResults.region = endpointRegion;
        queryResults.LuisAppID = LUISappID;
        queryResults.LuisSubscriptionKey = LUISsubscriptionKey;
        queryResults.BingSpellCheckKey = BingSpellCheckKey;
        queryResults.success = "true";

        // send queryResults to ApplicationInsights
        LUIS.ApplicationInsightsTraceSuccess(dependencyTelemetry, queryResults);

        return LUIS_response;

    }
    private static void ApplicationInsightsTraceSuccess(DependencyTelemetry telemetry, LUISPrediction queryResults)
    {
        telemetry.Properties.Add("region", queryResults.region);
        telemetry.Properties.Add("query", queryResults.Query);
        telemetry.Properties.Add("success", queryResults.success);
        telemetry.Properties.Add("errorMessage", queryResults.errorMessage);
        telemetry.Properties.Add("alteredQuery", queryResults.alteredQuery);
        telemetry.Properties.Add("LuisAppID", queryResults.LuisAppID);
        telemetry.Properties.Add("LuisSubscriptionKey", queryResults.LuisSubscriptionKey);
        telemetry.Properties.Add("BingSpellCheckKey", queryResults.BingSpellCheckKey);

        // topScoringIntent
        telemetry.Properties.Add("topScoringintent_intent", queryResults.topScoringIntent.intent);
        telemetry.Properties.Add("topScoringintent_score", queryResults.topScoringIntent.score.ToString());

        // entities
        for (int i = 0;i< queryResults.entities.Count;i++)
        {
            telemetry.Properties.Add("entity_" + (i + 1).ToString() + "_entity", queryResults.entities[i].entity);
            telemetry.Properties.Add("entity_" + (i + 1).ToString() + "_type", queryResults.entities[i].type);
            telemetry.Properties.Add("entity_" + (i + 1).ToString() + "_startIndex", queryResults.entities[i].startIndex.ToString());
            telemetry.Properties.Add("entity_" + (i + 1).ToString() + "_endIndex", queryResults.entities[i].endIndex.ToString());
            telemetry.Properties.Add("entity_" + (i + 1).ToString() + "_score", queryResults.entities[i].score.ToString());
        }

        // intents
        for (int i = 0; i < queryResults.intents.Count; i++)
        {
            telemetry.Properties.Add("intent_" + (i + 1).ToString() + "_intent", queryResults.intents[i].intent);
            telemetry.Properties.Add("intent_" + (i + 1).ToString() + "_score", queryResults.intents[i].score.ToString());
        }
        telemetryClient.Track(telemetry);
    }
    private static void ApplicationInsightsTraceError(DependencyTelemetry telemetry, LUISPrediction queryResults)
    {
        telemetry.Properties.Add("region", queryResults.region);
        telemetry.Properties.Add("query", queryResults.Query);
        telemetry.Properties.Add("success", queryResults.success);
        telemetry.Properties.Add("errorMessage", queryResults.errorMessage);
        telemetry.Properties.Add("LuisAppID", queryResults.LuisAppID);
        telemetry.Properties.Add("LuisSubscriptionKey", queryResults.LuisSubscriptionKey);

        telemetryClient.Track(telemetry);        
    }
}

// MAIN ENTRY INTO AZURE FUNCTION
public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
    log.Info(fnName);

    // parse query parameter
    string query = req.GetQueryNameValuePairs()
        .FirstOrDefault(q => string.Compare(q.Key, "query", true) == 0)
        .Value;

    string region = req.GetQueryNameValuePairs()
        .FirstOrDefault(q => string.Compare(q.Key, "region", true) == 0)
        .Value;

    // Get request body
    dynamic data = await req.Content.ReadAsAsync<object>();

    // Set name to query string or body data
    query = query ?? data?.query;
    region = region ?? data?.region;

    log.Info("query = " + query);
    log.Info("region = " + region);

    return await LUIS.EndpointQuery(req, log, query, region);
}
