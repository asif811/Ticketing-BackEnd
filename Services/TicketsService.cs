using Amazon.SecurityToken.Model;
using Eapproval.Models;
using Eapproval.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Org.BouncyCastle.Tls;
using System.Linq;
using Eapproval.Helpers;
// using System.Text.Json;

namespace Eapproval.services;

public class TicketsService
{

    private readonly IMongoCollection<Tickets> _tickets;
    private readonly TeamsService _teamsService;
    private readonly CounterService _counterService;


    

    private HelperClass _helperClass;

    

    public TicketsService(TeamsService teamsService, CounterService counterService, HelperClass helperClass)
    {
        _teamsService = teamsService;
        _counterService = counterService;
        _helperClass = helperClass;
        var mongoClient = new MongoClient("mongodb://localhost:27017");
        var mongoDatabase = mongoClient.GetDatabase("eapproval");
        _tickets = mongoDatabase.GetCollection<Tickets>("tickets");
        
    }

    
    public async Task<JsonResult> GetHandlerStats(){

        IMongoQueryable<Tickets> _ticketsQueryable = _tickets.AsQueryable();
        var result = await _ticketsQueryable.Where(r => r.AssignedTo != null).GroupBy( r => r.AssignedTo.EmpName ).Select(r => new {
                        Handler = r.Key, Count = r.Count()
                     }).ToListAsync();

        return new JsonResult(result);
    } 


    public async Task<JsonResult> GetTicketsByDepartment(string department, string location){
          
        IMongoQueryable<Tickets> query = _tickets.AsQueryable();
         
          if(department != "all"){
            query = query.Where( d => d.Department == department);
          }


          if(location != "all"){
            query = query.Where(d => d.Location == location);
          }

        var result = await query.GroupBy(r => r.Department).Select(r => new {
            Department = r.Key, Count = r.Count()
        }).ToListAsync();

        return new JsonResult(result);
    }
 
    
    public async Task<JsonResult> GetTicketsByStatus(){
        var _ticketsQueryable  = _tickets.AsQueryable(); 
        var result = await _ticketsQueryable.GroupBy(r => r.Status).Select(r => new {
            Status = r.Key, Count = r.Count()
        }).ToListAsync();

        return new JsonResult(result);
    }

    public async Task<List<TicketsProjected>> GetProjectedTicketsForHandlers(User user)
    {
        var projections = Builders<Tickets>.Projection
                          .Include(ticket => ticket.Id)
                          .Include(ticket => ticket.RequestDate)
                          .Include(ticket => ticket.Number)
                          .Include(ticket => ticket.RaisedBy)

                          .Include(ticket => ticket.CurrentHandler)

                          .Include(ticket => ticket.ProblemDetails)
                          .Include(ticket => ticket.Status);
        var results = await _tickets.Find(new BsonDocument()).Project(projections).ToListAsync();
        List<TicketsProjected> mappedResults = new List<TicketsProjected>();
        
        foreach (var result in results)
        {
            var newTicket = new TicketsProjected()
            {
                Id = result["_id"].ToString(),
                ProblemDetails = result["problemDetails"].ToString(),
                RaisedByEmail = result["raisedBy"]["mailAddress"].ToString(),
                RaisedByName = result["raisedBy"]["empName"].ToString(),
                CurrentHandlerEmail = result["currentHandler"]["mailAddress"].ToString(),
                CurrentHandlerName = result["currentHandler"]["empName"].ToString(),
                Number = result["number"].ToInt32(),
                Status = result["status"].ToString()
            };

            mappedResults.Add(newTicket);
        }

        

        return mappedResults;
    }


 public void BulkReplaceTickets(List<Tickets> replacements)
    {
        var bulkWrites = new List<WriteModel<Tickets>>();

        foreach (var replacement in replacements)
        {
            var filter = Builders<Tickets>.Filter.Eq(x => x.Id, replacement.Id); // Assuming 'Id' is your unique identifier
            var replacementModel = new ReplaceOneModel<Tickets>(filter, replacement)
            {
                IsUpsert = true // Set to true if you want to insert the document if it doesn't exist
            };

            bulkWrites.Add(replacementModel);
        }

        var bulkWriteOptions = new BulkWriteOptions { IsOrdered = false }; // Set IsOrdered to false for unordered bulk write

        var result = _tickets.BulkWrite(bulkWrites, bulkWriteOptions);

        // Access result if needed (result.ProcessedRequests, result.ModifiedCount, etc.)
    }

   public async Task<List<Tickets>> GetTicketsForMonitors(User user){
    var teams = await _teamsService.GetTeamsForMonitors(user);
    var results = await _tickets.Find((x)=> teams.Any((y)=> y.Name == x.Department)).ToListAsync();
    return results;
   }

    public async Task<List<Tickets>> GetTicketsForHandler(User user) =>
    await _tickets.Find(ticket => (ticket.Users.Any(x => x == user.MailAddress) || ticket.TicketingHead.MailAddress == user.MailAddress ||  ticket.RaisedBy.MailAddress == user.MailAddress) && ticket.Location == user.Location).ToListAsync();


    public async Task<List<Tickets>> GetTicketsForHandler2(User user){

   var results = await _tickets.Find(ticket => (ticket.Users != null && ticket.Users.Any(x => x == user.MailAddress)) || (ticket.TicketingHead != null && ticket.TicketingHead.MailAddress == user.MailAddress) || (ticket.RaisedBy != null && ticket.RaisedBy.MailAddress == user.MailAddress )|| 
   (ticket.CurrentHandler != null && ticket.CurrentHandler.MailAddress == user.MailAddress) || (ticket.Mentions != null && ticket.Mentions.Any(x => x == user.EmpName))).ToListAsync();
    return results;
    }
    public async Task<List<Tickets>> GetDepartmentTickets(string userMail)
    {
        var teams = await _teamsService.GetTeamsForHead(userMail);
        var results = await _tickets.Find(ticket => teams.Any(
            team => team.HasServices.HasValue && team.HasServices == true ?
            team.Services.Any(service => service.ServiceName == ticket.ServiceType) :
            ticket.Department == team.Name
        )
        ).ToListAsync();
        return results;
    }

    public async Task<List<Tickets>> GetDepartmentHeadTickets(User user){
        var teams = await _teamsService.GetTeamsForDepartmentHead(user.MailAddress);
        var results = await _tickets.Find(ticket => teams.Any(team => team.Name == ticket.Department)).ToListAsync();
        return results;
    }

    public async Task<List<Tickets>> GetTicketsRaisedByUser(User user)
    {
        var results = await _tickets.Find(ticket => ticket.RaisedBy.EmpName == user.EmpName).ToListAsync();
        return results;
    }

    public async Task<Tickets?> GetAsync(string id) =>
        await _tickets.Find(x => x.Id == id).FirstOrDefaultAsync();

    public async Task CreateAsync(Tickets newTicket) {
        var serial = await _counterService.GetOrCreateCounterAsync();
        newTicket.Number = serial;
        await _tickets.InsertOneAsync(newTicket);
        return;
    }


    public async Task<List<Tickets>> GetAllTickets() =>
   await _tickets.Find(_ => true).ToListAsync();



    public async Task UpdateAsync(string id, Tickets updatedTicket) =>
        await _tickets.ReplaceOneAsync(x => x.Id == id, updatedTicket);


         public async Task<ReplaceOneResult> UpdateAsyncReturn(string id, Tickets updatedTicket){


        var result = await _tickets.ReplaceOneAsync(x => x.Id == id, updatedTicket);
           return result;
         }

    public async Task RemoveAsync(string id) =>
        await _tickets.DeleteOneAsync(x => x.Id == id);

    public async Task<Tickets?> GetOneTicketByGroups(string group) =>   
        await _tickets.Find(x => x.Groups.Contains(group)).FirstOrDefaultAsync();


    public async Task<List<Tickets>> GetTicketsForLeader(User user)
    {
        var results = await _tickets.Find(x => x.TicketingHead.EmpName == user.EmpName || x.TicketingHead.MailAddress == user.MailAddress).ToListAsync();
        return results;

    }

    public async Task<List<Tickets>> GetTicketsForNormal(User user)
    {
        var results = await _tickets.Find(x => x.RaisedBy.EmpName == user.EmpName || x.RaisedBy.MailAddress == user.MailAddress).ToListAsync();
        return results;
    }


    public async Task<List<Tickets>> GetTicketsForSupport(User user)
    {
        var results = await _tickets.Find(x => (x.AssignedTo.EmpName == user.EmpName || x.AssignedTo.MailAddress == user.EmpName) && x.Accepted == true).ToListAsync();
        return results;
    }



    public async Task<List<Tickets>> GetTicketsByDepartment(List<string> departments){
        var results = await _tickets.Find(x => departments.Contains(x.Department) && x.Status != "Closed Ticket").ToListAsync();
        return results;
    }



}
