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

namespace Eapproval.Controllers;

    [ApiController]
    [Route("/")]
    public class ChartController : Controller
    {

        HelperClass _helperClass;
        TicketsService _ticketsService;
        TicketMailer _ticketMailer;
        FileHandler _fileHandler;
        Notifier _notifier;
        UsersService _usersService;
        TeamsService _teamsService;
       
     
        

        public ChartController(TeamsService teamsService, UsersService usersService, HelperClass helperClass, TicketsService ticketsService, TicketMailer ticketMailer, FileHandler fileHandler, Notifier notifier)
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
        [Route("getHandlerStats")]
        public async Task<IActionResult> GetHandlerStats(IFormCollection data)
        {
               var results = await _ticketsService.GetHandlerStats();
               return Ok(results);

        }



        
        [HttpPost]
        [Route("getTicketsByDepartment")]
        public async Task<IActionResult> GetTicketsByDepartment(IFormCollection data)
        {
               var results = await _ticketsService.GetTicketsByDepartment(data["department"], data["location"]);
               return Ok(results);

        }


        [HttpPost]
        [Route("getTicketsByStatus")]
        public async Task<IActionResult> GetTicketsByStatus(IFormCollection data)
        {
               var results = await _ticketsService.GetTicketsByStatus();
               return Ok(results);

        }


    }