﻿using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;
using Org.BouncyCastle.Asn1.Mozilla;

namespace Eapproval.Models;




public class ProblemTypesClass{
    
    [BsonElement("name")]
    [JsonPropertyName("name")]
    public string Name { get; set; } = "Not Given";


    [BsonElement("subs")]
    [JsonPropertyName("subs")]
    public List<string> Subs { get; set; } = new List<string>();


}

public class SubordinatesClass
{


    [BsonElement("user")]
    [JsonPropertyName("user")]
    public User? User { get; set; } = null;


    [BsonElement("rank")]
    [JsonPropertyName("rank")]
    public int? Rank { get; set; } = 2;
}




public class Services
{
    [BsonElement("serviceName")]
    [JsonPropertyName("serviceName")]
    public string ServiceName { get; set; } = "Not Available";


    [BsonElement("serviceLeader")]
    [JsonPropertyName("serviceLeader")]
    public User? ServiceLeader { get; set; } = null;



    [BsonElement("subordinates")]
    [JsonPropertyName("subordinates")]
    public List<SubordinatesClass>? Subordinates { get; set; } = null;


    [BsonElement("details")]
    [JsonPropertyName("details")]
    public List<DetailsClass>? Details { get; set; } = null;




}

public class Team
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [BsonElement("_id")]
    [JsonPropertyName("_id")]
    public string? Id { get; set; } 


   

    [BsonElement("hasServices")]
    [JsonPropertyName("hasServices")]
    public bool? HasServices { get; set; } = false;


    [BsonElement("name")]
    [JsonPropertyName("name")]
    public string? Name { get; set; } = "Not Available";


    [BsonElement("leaders")]
    [JsonPropertyName("leaders")]
    public List<User>? Leaders { get; set; } = null;


    [BsonElement("problemTypes")]
    [JsonPropertyName("problemTypes")]
    public List<ProblemTypesClass>? ProblemTypes { get; set; } = null;





    [BsonElement("monitors")]
    [JsonPropertyName("monitors")]
    public List<User>? Monitors { get; set; } = null;


     [BsonElement("head")]
    [JsonPropertyName("head")]
    public User? Head { get; set; } = null;





    [BsonElement("subordinates")]
    [JsonPropertyName("subordinates")]
    public List<SubordinatesClass>? Subordinates { get; set; } = null;


    [BsonElement("services")]
    [JsonPropertyName("services")]
    public List<Services>? Services { get; set; } = null;

    [BsonElement("details")]
    [JsonPropertyName("details")]
    public List<DetailsClass>? Details { get; set; } = null;


}



