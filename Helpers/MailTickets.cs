using System;
using System.Collections.Generic;
using System.Linq;
using MimeKit;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using Eapproval.services;
using System.Diagnostics;
using Eapproval.Models;


namespace Eapproval.Helpers;
public class MailTicket
{

    UsersService _usersService;
    TicketsService _ticketsService;

    HelperClass _helperClass;

    public MailTicket(UsersService usersService, TicketsService ticketsService, HelperClass helperclass){
         _usersService = usersService;
         _ticketsService = ticketsService;
         _helperClass = helperclass;
    }

    public async Task GetNewTickets()
    {
        // Set your Gmail credentials and server information
  
        string server = "imap.gmail.com";
        int port = 993;

        using (var client = new ImapClient())
        {
            // Connect to the server
            client.Connect(server, port, true);

            // Log in to your Gmail account
            client.Authenticate(email, password);

            // Open the Inbox folder
            var inbox = client.Inbox;
            inbox.Open(FolderAccess.ReadWrite);

            // Search for unread messages
            var searchQuery = SearchQuery.Not(SearchQuery.Seen);
            var unreadMessages = inbox.Search(searchQuery);

            // Retrieve and process unread messages
            foreach (var uid in unreadMessages)
            {
                var message = inbox.GetMessage(uid);
                var body = GetTextBodyWithoutSignature(message);
          
                var senderMail = message.From.Mailboxes.FirstOrDefault()?.Address;

                var currentDate = _helperClass.GetCurrentTime();
                
                if(senderMail != null){
                    var user = await _usersService.GetUserByMail(senderMail);
                    if(user != null){
                        var ticket = new Tickets(){
                            RaisedBy = user,
                            ProblemDetails = body
                        };
                          ticket.InitialLocation = "";
                          ticket.InitialPriority = null;
                          ticket.InitialType = "";
                          ticket.RequestDate = currentDate;
                          ticket.Actions = new List<ActionObject>();
                          ticket.MadeCloseRequest = false;
                          ticket.PrevHandler = user;
                          ticket.Source = "email";

                          var action = await  _helperClass.GetAction(ticket.Actions, user, ticket.CurrentHandler, "Not Available", ActionType.TicketRaised, null); 
                          ticket.Actions.Add(action);

                        _ticketsService.CreateAsync(ticket);


                    }
                }

                // Process the unread message as needed 
          

                // Mark the message as read
                inbox.AddFlags(uid, MessageFlags.Seen, true);
            }

            // Disconnect from the server
            client.Disconnect(true);
        }
    }





private static string GetTextBodyWithoutSignature(MimeMessage message)
{
    // Use the TextBody property to get the text content
    var textBody = message.TextBody;

    // Use MimeKit's TextPart.GetText() method to extract text content without the signature
    if (message.Body is TextPart textPart)
    {
        var text = textPart.Text;

        // Modify this part to exclude the signature
        // For example, you might want to remove everything after a certain marker or pattern
        // In this example, we'll remove lines starting with "-- " (common signature delimiter)
        var lines = text.Split('\n');
        var filteredLines = lines.TakeWhile(line => !line.StartsWith("-- "));

        return string.Join('\n', filteredLines);
    }

    return textBody;
}
}