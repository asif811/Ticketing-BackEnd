using Eapproval.Helpers;
using Microsoft.AspNetCore.Mvc;
using Eapproval.Models;
using Eapproval.services;
using System.Text.Json;
using MongoDB.Bson;
using System.Runtime.CompilerServices;
using MongoDB.Driver.Core.Authentication;
using Org.BouncyCastle.Ocsp;
using System.IO;
using MongoDB.Driver.Core.Operations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Formatters;
using System.Text;
using Eapproval.Services;
using MailKit;
using Microsoft.AspNetCore.Authorization;

namespace Eapproval.Controllers
{
    [ApiController]
    [Route("/")]
    public class TicketController : Controller
    {

        HelperClass _helperClass;
        TicketsService _ticketsService;
        TicketMailer _ticketMailer;
        FileHandler _fileHandler;
        Notifier _notifier;
        UsersService _usersService;
        TeamsService _teamsService;
       
     
        

        public TicketController(TeamsService teamsService, UsersService usersService, HelperClass helperClass, TicketsService ticketsService, TicketMailer ticketMailer, FileHandler fileHandler, Notifier notifier)
        {
            _helperClass = helperClass;
            _ticketsService = ticketsService; 
            _ticketMailer = ticketMailer;
            _fileHandler = fileHandler;
            _notifier = notifier;
            _usersService = usersService;
            _teamsService = teamsService;
        }




        [HttpPost]
        [Route("submitTicket")]
        public async Task<IActionResult> SubmitTicket(IFormCollection data)
        {
            var result = await _helperClass.GetContent(data);
            var ticket = result.ticket;
            var comment = result.comment;
            var user = result.user;
            var fileNames = result.fileNames;
            ticket.Actions = new List<ActionObject>();
            ticket.RaisedBy = user;
            ticket.MadeCloseRequest = false;
            string message;


            ticket.PrevHandler = user;
            EventType mailEvent;
            
            var ticketingHeads = await _helperClass.GetTicketingHeads(ticket);
            var thisTicketHead = ticketingHeads.FirstOrDefault(x => x.Location == ticket.Location);
            ticket.TicketingHead = thisTicketHead;

            if (ticket.Type == "service")
            {
                ticket.CurrentHandler = null;
                 mailEvent = EventType.SeekSupervisorApproval;
                ticket.ApprovalRequired = true;
                ticket.Status = "Ticket Submitted - Seeking Department Head's Approval";
                message = $"{user.EmpName} is asking for you approval to raise a service ticket for {ticket.Department} ";
              
            }
            else
            {
                ticket.CurrentHandler = null;
                mailEvent = EventType.SeekHigherAuthorityApproval;
                ticket.ApprovalRequired = false;
                ticket.Status = "Ticket Submitted";
                message = $"{user.EmpName} has raised a ticket for {ticket.Department} ";

            }

            ticket.RequestDate = _helperClass.GetCurrentTime();



            var action = await  _helperClass.GetAction(ticket.Actions, user, ticket.CurrentHandler, comment, ActionType.TicketRaised, file:fileNames); 
            
            ticket.Actions.Add(action);


            var subordinates = await _teamsService.GetConcernedUsers(ticket.Department);
              
        

            if(ticket.Genesis == false){
                var genesisTicket = await _ticketsService.GetAsync(ticket.GenesisId);
                genesisTicket.TimesRaised++;
                ticket.TimesRaised = genesisTicket.TimesRaised;
                await _ticketsService.UpdateAsync(genesisTicket.Id, genesisTicket);
            };


            ticket.InitialLocation = ticket.Location;
            ticket.InitialPriority = ticket.Priority;
            ticket.InitialType = ticket.TicketType;
            

            await _ticketsService.CreateAsync(ticket);



            foreach (var subordinate in subordinates)
            {
                _notifier.InsertNotification(action.Time, message, user, subordinate.User, ticket.Id);
                ticket.Users.Add(subordinate.User.MailAddress);

            }

            await _ticketsService.UpdateAsync(ticket.Id, ticket);


         
             var members = await _teamsService.GetTeamByName(ticket.Department);




            //  foreach(var x in members.Subordinates){
            //        _ticketMailer.SendMail(user, x.User, ticket.Department, "new ticket", ticket.Id, user);

            //  }

       

           


       


            



            return Ok(true);
          


        }



        
        [HttpPost]
        [Route("reOpen")]
        public async Task<IActionResult> ReOpen(IFormCollection data)
        {
           var result = await _helperClass.GetContent(data);
           var ticket = result.ticket;
           var comment = result.comment;
           var user = result.user;
           var fileNames = result.fileNames;;

           ticket.CurrentHandler = null;
           ticket.Status = "Ticket Submitted";
           ticket.MadeCloseRequest = false;


            EventType mailEvent;

           var message = $"{user.EmpName} has opened a ticket for the {ticket.Department} ";

         

           var action = await  _helperClass.GetAction(ticket.Actions, user, ticket.CurrentHandler, comment, ActionType.ReOpen, file:fileNames); 
           
           ticket.Actions.Add(action);

           ticket.AssignedTo = null;
           ticket.Genesis = false;
           ticket.GenesisId = ticket.Id;
           ticket.Id = null;
           var genesisTicket = await _ticketsService.GetAsync(ticket.GenesisId);
           genesisTicket.TimesRaised++;
           ticket.TimesRaised = genesisTicket.TimesRaised;

           var tickets = await _ticketsService.GetTicketsForHandler(user);

           var oldTicket = tickets.FirstOrDefault(x=>(x.GenesisId == ticket.GenesisId && x.Status != "Closed Ticket") || (x.Id == ticket.GenesisId && x.Status != "Closed Ticket"));

           if(oldTicket != null){
            return Ok("exists");
           }else{

           

           await _ticketsService.UpdateAsync(genesisTicket.Id, genesisTicket);
           await _ticketsService.CreateAsync(ticket);

             var subordinates = await _teamsService.GetConcernedUsers(ticket.Department);
                 foreach (var subordinate in subordinates)
            {
                _notifier.InsertNotification(action.Time, message, user, subordinate.User, ticket.Id);
              

            }


            
             var members = await _teamsService.GetTeamByName(ticket.Department);




            //  foreach(var x in members.Subordinates){
            //        _ticketMailer.SendMail(user, x.User, ticket.Department, "new ticket", ticket.Id, user);

            //  }

        



            return Ok(ticket.Id);
          
           }

        }




        [HttpPost]
        [Route("supervisorApprove")]
        public async Task<IActionResult> SupervisorApprove(IFormCollection data)
        {
            var result = await _helperClass.GetContent(data);
            var user = result.user;
            var ticket = result.ticket;
            var comment = result.comment;
            var files = result.fileNames;

            ticket.PrevHandler = user;
            ticket.CurrentHandler = ticket.TicketingHead;
            ticket.Files = files;
            ticket.ApprovalRequired = false;

            ticket.Status = "Ticket Submitted - Department Head's Approval Given";
            ticket.MadeCloseRequest = false;

            var action = await _helperClass.GetAction(ticket.Actions, user, ticket.CurrentHandler, comment, ActionType.SupervisorApprove);

            ticket.Actions.Add(action);

            await _ticketsService.UpdateAsync(ticket.Id, ticket);


            var message = $"{user.EmpName} has approved a ticket to be raised for {ticket.Department}";

            _notifier.InsertNotification(action.Time, message, user, ticket.CurrentHandler, ticket.Id);

            // _ticketMailer.SendMail(user, ticket.CurrentHandler, ticket.Department, "approved", ticket.Id, user);

            
            
            
            return Ok(true);

        }



        [HttpPost]
        [Route("askApproval")]
        public async Task<IActionResult> SeekHigherApproval(IFormCollection data)
        {
            var result = await _helperClass.GetContent(data);
            var user = result.user;
            var ticket = result.ticket;
            var comment = result.comment;
            var approver = JsonSerializer.Deserialize<User>(data["approver"]);
            
            ticket.PrevHandler = user;
            ticket.CurrentHandler = approver;
            ticket.MadeCloseRequest = false;
            ticket.Status = "Ticket Submitted - Seeking Additional Approval";
            ticket.ApprovalRequired = true;
          
            var action = await _helperClass.GetAction(ticket.Actions, user, ticket.CurrentHandler, comment, ActionType.SeekingHigherApproval);
            ticket.Actions.Add(action);

            await _ticketsService.UpdateAsync(ticket.Id, ticket);
           
            var message = $"{user.EmpName} is seeking your approval to deal with a ticket raised for {ticket.Department}";

            _notifier.InsertNotification(action.Time, message, user, ticket.CurrentHandler, ticket.Id);


            // _ticketMailer.SendMail(user, ticket.CurrentHandler, ticket.Department, "seeking approval", ticket.Id, user);

            return Ok(true);

        }








        [HttpPost]
        [Route("higherApprove")]
        public async Task<IActionResult> HigherApprove(IFormCollection data)
        {
            var result = await _helperClass.GetContent(data);
            var user = result.user;
            var ticket = result.ticket;
            var comment = result.comment;
            var fileNames = result.fileNames;

            ticket.Status = "Ticket Submitted - Additional Approval Given";
            ticket.CurrentHandler = ticket.PrevHandler;
            ticket.PrevHandler = user;

            ticket.ApprovalRequired = false;
          
          
            ticket.MadeCloseRequest = false;
            var action = await _helperClass.GetAction(ticket.Actions, user, ticket.CurrentHandler, comment, ActionType.HigherApprove, file:fileNames);
            ticket.Actions.Add(action);
            await _ticketsService.UpdateAsync(ticket.Id, ticket);
          

            var message = $"{user.EmpName} has approved the ticket raised for {ticket.Department}";

            _notifier.InsertNotification(action.Time, message, user, ticket.CurrentHandler, ticket.Id);


            // _ticketMailer.SendMail(user, ticket.CurrentHandler, ticket.Department, "approved", ticket.Id, user);
            return Ok(true);

            

        }




        

        [HttpPost]
        [Route("assignSelf")]
        public async Task<IActionResult> AssignSelf(IFormCollection data)
        {

            var user = JsonSerializer.Deserialize<User>(data["user"]);
            var ticket = JsonSerializer.Deserialize<Tickets>(data["ticket"]);
            var comment = data["comment"];


            await _usersService.UpdateUserNumber(user);
            ticket.Assigned = true;
            ticket.AssignedTo = user;
            ticket.PrevHandler = ticket.CurrentHandler;
            ticket.CurrentHandler = ticket.AssignedTo;
            ticket.Accepted = true;
            ticket.Status = "Assigned";
            ticket.MadeCloseRequest = false;
            var action = await _helperClass.GetAction(ticket.Actions, user, ticket.CurrentHandler, comment, ActionType.AssignSelf);
            ticket.Actions.Add(action);
            await _ticketsService.UpdateAsync(ticket.Id, ticket);
          
            return Ok(ticket);


        }





        [HttpPost]
        [Route("assign")]
        public async Task<IActionResult> AssignOther(IFormCollection data)
        {       

            var ticket = JsonSerializer.Deserialize<Tickets>(data["ticket"]);
            
            var approver = JsonSerializer.Deserialize<User>(data["approver"]);
            var comment = data["comment"];
            var user = JsonSerializer.Deserialize<User>(data["user"]);

            await _usersService.UpdateUserNumber(approver);
            ticket.PrevHandler = user;
            ticket.CurrentHandler = approver;
            ticket.Assigned = true;
            ticket.AssignedTo = approver;
            ticket.MadeCloseRequest = false;
            ticket.Status = "Assigned";
            ticket.Accepted = false;
            ticket.Location = ticket.AssignedTo.Location;
       
            var action = await _helperClass.GetAction(ticket.Actions, user, ticket.CurrentHandler, comment, ActionType.AssignOther);
            ticket.Actions.Add(action);
            await _ticketsService.UpdateAsync(ticket.Id, ticket);


            var message = $"{user.EmpName} has assigned you the ticket raised for {ticket.Department} by {ticket.RaisedBy.EmpName}";

            _notifier.InsertNotification(action.Time, message, user, ticket.CurrentHandler, ticket.Id);

            // _ticketMailer.SendMail(user, ticket.CurrentHandler, ticket.Department, "assigned", ticket.Id, user);
            return Ok(true);



        }

        [HttpPost]
        [Route("unassign")]
        public async Task<IActionResult> Unassign(IFormCollection data)
        {

            var ticket = JsonSerializer.Deserialize<Tickets>(data["ticket"]);

            var prevassignee = JsonSerializer.Deserialize<User>(data["prevAssignee"]);
            var comment = data["comment"];
            var user = JsonSerializer.Deserialize<User>(data["user"]);

           
            ticket.PrevHandler = prevassignee;
            ticket.CurrentHandler = null;
            ticket.Assigned = false;
            ticket.AssignedTo = null;
            ticket.MadeCloseRequest = false;
            ticket.Status = "Ticket Submitted";
            ticket.Accepted = false;
            
        
            var action = await _helperClass.GetAction(ticket.Actions, user, ticket.CurrentHandler, comment, ActionType.Unassigned);
            ticket.Actions.Add(action);
            await _ticketsService.UpdateAsync(ticket.Id, ticket);


            var message = $"{user.EmpName} has unassigned a ticket from you which was raised for {ticket.Department} by {ticket.RaisedBy.EmpName}";

            _notifier.InsertNotification(action.Time, message, user, prevassignee, ticket.Id);

            //  _ticketMailer.SendMail(user, ticket.PrevHandler, ticket.Department, "unassigned", ticket.Id, user);
            return Ok(true);



        }







        [HttpPost]
        [Route("reassign")]
        public async Task<IActionResult> Reassign(IFormCollection data)
        {
            var user = JsonSerializer.Deserialize<User>(data["user"]);
            var ticket = JsonSerializer.Deserialize<Tickets>(data["ticket"]);
            var approver = JsonSerializer.Deserialize<User>(data["approver"]);
            var comment = data["comment"];

            ticket.PrevHandler = ticket.CurrentHandler;
            ticket.CurrentHandler = approver;
            ticket.Assigned = true;
            ticket.AssignedTo = approver;
            ticket.MadeCloseRequest = false;
            ticket.Status = "Assigned";
            ticket.Location = ticket.AssignedTo.Location;
            ticket.Accepted = false;
            var action = await _helperClass.GetAction(ticket.Actions, user, ticket.CurrentHandler, comment, ActionType.AssignOther);
            ticket.Actions.Add(action);
            await _ticketsService.UpdateAsync(ticket.Id, ticket);

            var message = $"{user.EmpName} has assigned you the ticket raised for {ticket.Department} by {ticket.RaisedBy.EmpName}";

            _notifier.InsertNotification(action.Time, message, user, ticket.CurrentHandler, ticket.Id);

            // _ticketMailer.SendMail(user, ticket.CurrentHandler, ticket.Department, "assigned", ticket.Id, user);
            return Ok(true);


        }



        [HttpPost]
        [Route("askInfo")]
        public async Task<IActionResult> AskInfo(IFormCollection data)
        {
            var user = JsonSerializer.Deserialize<User>(data["user"]);
            var comment = data["comment"];
            var ticket = JsonSerializer.Deserialize<Tickets>(data["ticket"]);
            var informer = JsonSerializer.Deserialize<User>(data["approver"]);
         



            ticket.PrevHandler = user;
            ticket.CurrentHandler = informer;


            ticket.Ask = true;
            ticket.MadeCloseRequest = false;
            ticket.Status = "Open (Seeking Information...)";
            var action = await _helperClass.GetAction(ticket.Actions, user, ticket.CurrentHandler, comment, ActionType.AskInfo);
            ticket.Actions.Add(action);
            await _ticketsService.UpdateAsync(ticket.Id, ticket);

            var message = $"{user.EmpName} is asking for more information regarding a ticket raised for {ticket.Department} by {ticket.RaisedBy.EmpName}";

            _notifier.InsertNotification(action.Time, message, user, ticket.CurrentHandler, ticket.Id);

            // _ticketMailer.SendMail(user, ticket.CurrentHandler, ticket.Department, "seeking information", ticket.Id, user);
            return Ok(true);


        }


        [HttpPost]
        [Route("giveInfo")]
        public async Task<IActionResult> GiveInfo(IFormCollection data)
        {
            var result = await _helperClass.GetContent(data);
            var ticket = result.ticket;
            var comment = result.comment;
            var user = result.user;
            var filenames = result.fileNames;
            var info = result.info;

            ticket.Ask = false;

            ticket.Status = "Open (Information Sent)";
            ticket.CurrentHandler = ticket.PrevHandler;
            ticket.PrevHandler = user;
            ticket.MadeCloseRequest = false;
            var action = await _helperClass.GetAction(ticket.Actions, user, ticket.CurrentHandler, comment, ActionType.GiveInfo, file:filenames, info: info);
            ticket.Actions.Add(action);
            await _ticketsService.UpdateAsync(ticket.Id, ticket);

            var message = $"{user.EmpName} has given you more information regarding the ticket raised for {ticket.Department} ";

            _notifier.InsertNotification(action.Time, message, user, ticket.CurrentHandler, ticket.Id);

            // _ticketMailer.SendMail(user, ticket.CurrentHandler, ticket.Department, "given information", ticket.Id, user);
            return Ok(true);

        }



        [HttpPost]
        [Route("closeRequest")]
        public async Task<IActionResult> CloseRequest(IFormCollection data)
        {
            var result = await _helperClass.GetContent(data);
            var user = result.user;
            var ticket = result.ticket;
            var comment = result.comment;
            var filenames = result.fileNames;
            var info = result.info;

            ticket.PrevHandler = ticket.CurrentHandler;
            ticket.CurrentHandler = ticket.RaisedBy;
            ticket.MadeCloseRequest = true;

            ticket.Status = "Close Requested";

            var action = await _helperClass.GetAction(ticket.Actions, user, ticket.CurrentHandler, comment, ActionType.CloseRequest, file:filenames, info:info);
            ticket.Actions.Add(action);
            await _ticketsService.UpdateAsync(ticket.Id, ticket);

            var message = $"{user.EmpName} has requested you to close the request you raised for {ticket.Department} ";

            _notifier.InsertNotification(action.Time, message, user, ticket.CurrentHandler, ticket.Id);


            // _ticketMailer.SendMail(user, ticket.CurrentHandler, ticket.Department, "closed", ticket.Id, user);
            return Ok(true);
        }




        [HttpPost]
        [Route("closeTicket")]
        public async Task<IActionResult> CloseTicket(IFormCollection data)
        {
            var result = await _helperClass.GetContent(data);
            var user = result.user;
            var ticket = result.ticket;
            var comment = result.comment;
            var filenames = result.fileNames;
            var rating = data["rating"];
            

            ticket.PrevHandler = ticket.CurrentHandler;
          

            ticket.Status = "Closed Ticket";
            ticket.MadeCloseRequest = false;

     
           
            var message = $"{user.EmpName} has closed the ticket you raised for the {ticket.Department} ";

            var action = await _helperClass.GetAction(ticket.Actions, user, ticket.CurrentHandler, comment, ActionType.TicketClosed, file:filenames);
            ticket.Actions.Add(action);
            await _ticketsService.UpdateAsync(ticket.Id, ticket);
            _notifier.InsertNotification(action.Time, message, user, ticket.RaisedBy, ticket.Id);
            // _ticketMailer.SendMail(user, ticket.RaisedBy, ticket.Department, "closed", ticket.Id, user);
            return Ok(true);
        }



        [HttpPost]
        [Route("rate")]
        public async Task<IActionResult> Rate(IFormCollection data)
        {
            var result = await _helperClass.GetContent(data);
            var user = result.user;
            var ticket = result.ticket;
            var comment = result.comment;
            var rating = data["rating"];
            

            ticket.PrevHandler = ticket.CurrentHandler;
            ticket.CurrentHandler = null;
        

            ticket.Status = "Closed Ticket";
            ticket.MadeCloseRequest = false;

            if (ticket.AssignedTo != null)
            {
                var handler = await _usersService.GetOneUser(ticket.AssignedTo.Id);
                var prevRating = handler.Rating;
                var prevRaters = handler.Raters;
                var newRating = int.Parse(rating);
                var newRaters = prevRaters + 1;
                var currentAvgRating = ((prevRaters * prevRating) + newRating) / newRaters;
                handler.Rating = currentAvgRating;
                handler.Raters = newRaters;

                _usersService.UpdateAsync(handler.Id, handler);

            }

           
            

            var action = await _helperClass.GetAction(ticket.Actions, user, ticket.CurrentHandler, comment, ActionType.Rated);
            ticket.Actions.Add(action);
            await _ticketsService.UpdateAsync(ticket.Id, ticket);
          
            return Ok(true);
        }








        [HttpPost]
        [Route("closeRequestReject")]
        public async Task<IActionResult> CloseRequestReject(IFormCollection data)
        {
            var result = await _helperClass.GetContent(data);
            var user = result.user;
            var ticket = result.ticket;
            var comment = result.comment;
            var info = result.info;
            

            ticket.PrevHandler = ticket.CurrentHandler;
            ticket.CurrentHandler = ticket.AssignedTo;

            ticket.Status = "Open";
            ticket.MadeCloseRequest = false;

            var action = await _helperClass.GetAction(ticket.Actions, user, ticket.CurrentHandler, comment, ActionType.CloseRequestReject, info:info);
            ticket.Actions.Add(action);
            await _ticketsService.UpdateAsync(ticket.Id, ticket);

            var message = $"{user.EmpName} has rejected your close request for the ticket he raised for {ticket.Department} ";

            _notifier.InsertNotification(action.Time, message, user, ticket.CurrentHandler, ticket.Id);

          
            return Ok(true);
        }




        [HttpPost]
        [Route("rejectTicket")]
        public async Task<IActionResult> RejectTicket(IFormCollection data)
        {
            var result = await _helperClass.GetContent(data);
            var user = result.user;
            var ticket = result.ticket;
            var comment = result.comment;
            


            ticket.PrevHandler = ticket.CurrentHandler;
            ticket.CurrentHandler = null;

            ticket.Status = "Rejected";

            ticket.MadeCloseRequest = false;

            var action = await _helperClass.GetAction(ticket.Actions, user, ticket.RaisedBy, comment, ActionType.Reject);
            ticket.Actions.Add(action);
            await _ticketsService.UpdateAsync(ticket.Id, ticket);


            var message = $"{user.EmpName} has rejected the ticket you raised for {ticket.Department}";

            _notifier.InsertNotification(action.Time, message, user, ticket.RaisedBy, ticket.Id);
            // _ticketMailer.SendMail(user, ticket.RaisedBy, ticket.Department, "rejected", ticket.Id, user);
            return Ok(true);
        }


        [HttpPost]
        [Route("getTickets")]
        public async Task<IActionResult> GetTickets(IFormCollection data)
        {
            var user = JsonSerializer.Deserialize<User>(data["user"]);

            var result =await _ticketsService.GetTicketsForHandler(user);

       
            return Ok(result);


        }



        [HttpPost]
        [Route("getTicketsForMonitors")]
        public async Task<IActionResult> GetTicketsForMonitors(IFormCollection data)
        {
            var user = JsonSerializer.Deserialize<User>(data["user"]);

            var result =await _ticketsService.GetTicketsForMonitors(user);

       
            return Ok(result);


        }


        [HttpPost]
        [Route("getTickets2")]
        public async Task<IActionResult> GetTickets2(IFormCollection data)
        {
            var user = JsonSerializer.Deserialize<User>(data["user"]);

            var result = await _ticketsService.GetTicketsForHandler2(user);


            return Ok(result);


        }


        [HttpPost]
        [Route("getTicket")]
        public async Task<IActionResult> GetTicket(IFormCollection data)
        {
            var id = data["id"];

            var result = await _ticketsService.GetAsync(id);

            return Ok(result);


        }


        

        [HttpPost]
        [Route("getAllTickets")]
        public async Task<IActionResult> GetAllTickets()
        {


            var result = await _ticketsService.GetAllTickets();

            return Ok(result);


        }

        [HttpPost]
        [Route("getDepartmentTickets")]
        public async Task<IActionResult> GetDepartmentTickets(IFormCollection data)
        {
            var user = JsonSerializer.Deserialize<User>(data["totalUser"]);

            List<Tickets>? results;

            if(user.UserType == "Ticket Manager (Department)"){
                 
                 results = await _ticketsService.GetDepartmentHeadTickets(user);

            }else{

                 results = await _ticketsService.GetDepartmentTickets(data["user"]);
            }

            return Ok(results);
        }

        

        [HttpPost]
        [Route("getTicketsRaisedByUser")]
        public async Task<IActionResult> GetTicketsRaisedByUser(IFormCollection data)
        {
            var user = JsonSerializer.Deserialize<User>(data["user"]);
            var results = await _ticketsService.GetTicketsRaisedByUser(user);
            return Ok(results);
        }


        [HttpPost]
        [Route("setPriority")]
        public async Task<IActionResult> SetPriority(IFormCollection data)
        {
            var results = await _ticketsService.GetAsync(data["id"]);
            results.Priority.Priority = data["priority"];
            results.Priority.ResolutionTime = Mappings.PriorityResolutionMap[data["priority"]];
            results.Priority.ResponseTime = Mappings.PriorityResponseMap[data["priority"]];
            await _ticketsService.UpdateAsync(data["id"], results);
            return Ok(results.Priority);
        }

        [HttpPost]
        [Route("setTicketType")]
        public async Task<IActionResult> SetTicketType(IFormCollection data)
        {
            var results = await _ticketsService.GetAsync(data["id"]);
            results.TicketType = data["ticketType"];
           
            await _ticketsService.UpdateAsync(data["id"], results);
            return Ok(results.TicketType);
        }


        [HttpPost]
        [Route("setLocation")]
        public async Task<IActionResult> SetLocation(IFormCollection data)
        {
            var results = await _ticketsService.GetAsync(data["id"]);
            results.Location = data["location"];
            results.Assigned = false;
            results.AssignedTo = null;
            results.CurrentHandler = null;

            var team = await _teamsService.GetTeamByName(results.Department);

            var ticketingHead = team.Leaders.Where( x => x.Location == results.Location).FirstOrDefault();
   
            results.TicketingHead = ticketingHead;
            await _ticketsService.UpdateAsync(data["id"], results);
            return Ok(results.Location);
        }



        
        [HttpPost]
        [Route("setDepartment")]
        public async Task<IActionResult> SetDepartment(IFormCollection data)
        {
            var results = await _ticketsService.GetAsync(data["id"]);
            
            results.Department = data["department"];
            results.Assigned = false;
            results.AssignedTo = null;
            results.CurrentHandler = null;

            var user = results.RaisedBy;

            var team = await _teamsService.GetTeamByName(results.Department);


            if(results.Location != null){

            var ticketingHead = team.Leaders.Where( x => x.Location == results.Location).FirstOrDefault();
            results.TicketingHead = ticketingHead;
            
            }


            var action = await _helperClass.GetAction(results.Actions, user, results.CurrentHandler, "Not Available", ActionType.TicketRaised, null);


           
            var message = $"A new ticket has been assigned to your department";

            var subordinates = await _teamsService.GetConcernedUsers(results.Department);

               foreach (var subordinate in subordinates)
            {
                _notifier.InsertNotification(action.Time, message, user, subordinate.User, results.Id);
                results.Users.Add(subordinate.User.MailAddress);

            }

          

   
            await _ticketsService.UpdateAsync(data["id"], results);
            return Ok(results.Location);
        }


        [HttpPost]
        [Route("getTicketsForLeader")]
        public async Task<IActionResult> GetTicketsForLeader(IFormCollection data)
        {
            var user = JsonSerializer.Deserialize<User>(data["user"]);
            var results = await _ticketsService.GetTicketsForLeader(user);
            return Ok(results);
           
        }

        [HttpPost]
        [Route("getTicketsForNormal")]
        public async Task<IActionResult> GetTicketsForNormal(IFormCollection data)
        {
            var user = JsonSerializer.Deserialize<User>(data["user"]);
            var results = await _ticketsService.GetTicketsForNormal(user);
            return Ok(results);

        }

        [HttpPost]
        [Route("getTicketsForSupport")]
        public async Task<IActionResult> GetTicketsForSupport(IFormCollection data)
        {
            var user = JsonSerializer.Deserialize<User>(data["user"]);
            var results = await _ticketsService.GetTicketsForSupport(user);
            return Ok(results);
        
        }



        
        [HttpPost]
        [Route("updateTicket")]
        public async Task<IActionResult> UpdateTicket(IFormCollection data)
        {
            var user = JsonSerializer.Deserialize<User>(data["user"]);
            var ticket = JsonSerializer.Deserialize<Tickets>(data["ticket"]);
            var results =  await _ticketsService.UpdateAsyncReturn(ticket.Id, ticket);
            if(results.IsAcknowledged == true){
                  return Ok(ticket);
            }else{
                return Ok(false);
            }
        
        
        }


        [HttpPost]
        [Route("updateTickets")]
        public async Task<IActionResult> UpdateTickets(IFormCollection data)
        {
          var tickets = JsonSerializer.Deserialize<List<Tickets>>(data["tickets"]);
          
          _ticketsService.BulkReplaceTickets(tickets);

          return Ok(true);


        
        }





    }
}
