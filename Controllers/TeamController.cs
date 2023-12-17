using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Eapproval.Models;
using Eapproval.services;
using ZstdSharp.Unsafe;
using Eapproval.Helpers;
using System.Xml.Serialization;
using Amazon.Auth.AccessControlPolicy;
using Microsoft.AspNetCore.Authorization;

namespace Eapproval.Controllers
{

    [ApiController]
    [Route("/")]
    public class TeamController:Controller
    {

        TeamsService _teamsService;
        HelperClass _helperClass;
        UsersService _usersService;

        TicketsService _ticketsService;


        public TeamController(TicketsService ticketsService, TeamsService teamsService, HelperClass helperClass, UsersService usersService)
        {
            _teamsService = teamsService;
            _helperClass = helperClass;
            _usersService = usersService;
            _ticketsService = ticketsService;
        }


        [HttpPost]
        [Route("/createTeam")]
        public async Task<IActionResult> CreateTeam(IFormCollection data)
        {
            var team = JsonSerializer.Deserialize<Team>(data["team"]);
         
            await _teamsService.CreateTeam(team);
            return Ok(true);
        }

        [HttpPost]
        [Route("/getTeams")]
        public async Task<IActionResult> GetTeams(IFormCollection data)
        {
            
            var result = await _teamsService.GetAllTeams();
            return Ok(result);
        }


         [HttpPost]
        [Route("/getTeamsForLeaders")]
        public async Task<IActionResult> GetTeamsForLeaders(IFormCollection data)
        {
            var email = data["user"];
            var user = JsonSerializer.Deserialize<User>(data["totalUser"]);

            List<Team>? result;
            if(user.UserType == "Ticket Manager (Department)"){
                   result = await _teamsService.GetTeamsForMonitors(user);
            }else{

             result = await _teamsService.GetTeamsForHead(email);
            }
            return Ok(result);
        }



        [HttpPost]
        [Route("/getDepartmentsAndTickets")]
        public async Task<IActionResult> GetDepartmentsAndTickets(IFormCollection data)
        {   
            var user = JsonSerializer.Deserialize<User>(data["user"]);
            var email = user.MailAddress;
            
            List<Team> teams;

            if(user.UserType == "Ticket Manager (Department)"){
                teams = await _teamsService.GetTeamsForDepartmentHead(email);
            }else{
               teams = await _teamsService.GetTeamsForHead(email);
            }

            List<string> departments = new List<string>();
            foreach(var t in teams){
                departments.Add(t.Name);
            };

            var tickets = await _ticketsService.GetTicketsByDepartment(departments);

            var result = new {
                departments = teams,
                tickets = tickets,
            };
            
            return Ok(result);
        }




        [HttpPost]
        [Route("/getTeam")]
        public async Task<IActionResult> GetTeam(IFormCollection data)
        {
            var id = data["id"];
            var result = await _teamsService.GetTeamById(id);
            return Ok(result);
        }





        [HttpPost]
        [Route("/editTeam")]
        public async Task<IActionResult> EditTeam(IFormCollection data)
        {
            var team = JsonSerializer.Deserialize<Team>(data["team"]);


           

             _teamsService.UpdateTeam(team.Id, team);
            return Ok(true);
        }




        [HttpPost]
        [Route("/deleteTeam")]
        public async Task<IActionResult> DeleteTeam(IFormCollection data)
        {
            var team = JsonSerializer.Deserialize<Team>(data["team"]);
            var id = team.Id;
            _teamsService.RemoveTeam(team.Id);
            return Ok(id);
        }



        [HttpPost]
        [Route("/updateRanks")]
        public async Task<IActionResult> UpdateRanks(IFormCollection data)
        {
            var team = JsonSerializer.Deserialize<Team>(data["team"]);

            await _teamsService.UpdateTeam(team.Id, team);

            return Ok(team);            

    


            return Ok(true);

        }




        [HttpPost]
        [Route("/getSupport")]
        public async Task<IActionResult> GetSupport(IFormCollection data)
        {
            var ticket = JsonSerializer.Deserialize<Tickets>(data["ticket"]);

            var result = await _helperClass.GetSupport(ticket);
            
            return Ok(result);
        }



        [HttpPost]
        [Route("/getAllSupport")]
        public async Task<IActionResult> GetAllSupport(IFormCollection data)
        {
           

            var result = await _teamsService.GetAllSupport();
            return Ok(result);
        }

        [HttpPost]
        [Route("/getSupportFromHead")]
        public async Task<IActionResult> GetSupportFromHead(IFormCollection data)
        {
            var user = JsonSerializer.Deserialize<User>(data["user"]);

            var result = await _teamsService.GetSupportFromHead(user);
            
            return Ok(result);
        }


         [HttpPost]
        [Route("/getSupportForDepartmentHead")]
        public async Task<IActionResult> GetSupportForDepartmentHead(IFormCollection data)
        {
            var user = JsonSerializer.Deserialize<User>(data["user"]);

            var result = await _teamsService.GetSupportForDepartmentHead(user);
            
            return Ok(result);
        }



        [HttpPost]
        [Route("/demoteTemporaryLeader")]
        public async Task<IActionResult> DemoteTemporaryLeader(IFormCollection data)
        {
            var user = JsonSerializer.Deserialize<User>(data["user"]);
            user.UserType = "support";

             await _usersService.UpdateAsync(user.Id, user);
            return Ok(true);
        }





    }
}
