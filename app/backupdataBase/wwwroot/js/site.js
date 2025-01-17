/*"use strict";*/

let connection = new signalR.HubConnectionBuilder().withUrl("/backupHub").build();
let backupName ;
//Disable the send button until connection is established.
 connection.on("ReceiveMessage", function (user, message) {
    var li = document.createElement("li");
    document.getElementById("messagesList").appendChild(li);
    // We can assign user-supplied strings to an element's textContent because it
    // is not interpreted as markup. If you're assigning in any other way, you 
    // should be aware of possible script injection concerns.
    li.textContent = `${user} says ${message}`;
});

connection.start().then(function () {
  /*  document.getElementById("sendButton").disabled = false;*/
}).catch(function (err) {
    return console.error(err.toString());
});


document.getElementById("backup").addEventListener("click", function (event) {

    $.get("/home/Backup", function (data, status) {

    });
    //connection.invoke("SendMessage", user, message).catch(function (err) {
    //    return console.error(err.toString());
    //});
    event.preventDefault();
});

document.getElementById("restore").addEventListener("click", function (event) {
     
    $.post("/home/Restore", {
        backupName
    }, function (data, status) {
        
    });
    //connection.invoke("SendMessage", user, message).catch(function (err) {
    //    return console.error(err.toString());
    //});
    event.preventDefault();
});


connection.on("GetBackUpProcess", function (proccess ) {
    document.getElementById('proccessBar').style.width = proccess + '%'; 
    proccessBar.setAttribute('aria-valuemin', proccess);
    document.getElementById('percent').innerHTML=proccess
});
document.getElementById('backupName').addEventListener('change', function () {

   
  backupName=  this.value
})