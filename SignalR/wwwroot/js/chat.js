"use strict";

var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : new P(function (resolve) { resolve(result.value); }).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};

const connection = new signalR.HubConnectionBuilder().withUrl("/k8sHub").build();

const term = new Terminal();
term.open(document.getElementById('terminal'));

const socket = new WebSocket("ws://localhost:5033/ws?workload=gwc-22z3vm7zd2oo");
socket.onmessage = (event) => {
    term.write(event.data);
}

term.onData(function (input) {
    socket.send(input);
})

term.focus();

document.getElementById("streamButton").addEventListener("click", (event) => __awaiter(this, void 0, void 0, function* () {
    try {
        connection.stream("Receive")
            .subscribe({
                next: (item) => {
                    console.log(item);
                    var li = document.createElement("pre");
                    li.textContent = item;
                    document.getElementById("messagesList").appendChild(li);
                },
                complete: () => {
                    var li = document.createElement("li");
                    li.textContent = "Stream completed";
                    document.getElementById("messagesList").appendChild(li);
                },
                error: (err) => {
                    var li = document.createElement("li");
                    li.textContent = err;
                    document.getElementById("messagesList").appendChild(li);
                },
            });
    }
    catch (e) {
        console.error(e.toString());
    }
    event.preventDefault();
}));

const validKeyCode = [13, 32]

document.getElementById("messageInput").addEventListener("keydown", (event) => __awaiter(this, void 0, void 0, function* () {

    console.log(event);
    const subject = new signalR.Subject();
    yield connection.send("Send", subject);
    if (event.key.toLowerCase() === 'enter') {
        subject.next('\n');
        document.getElementById("messageInput").value = '';
    } else if(event.key.toLowerCase() === 'Tab'){
        connection.send("Send", subject);
    }    
    else {
        subject.next(event.key);
    }
}));

// We need an async function in order to use await, but we want this code to run immediately,
// so we use an "immediately-executed async function"
(() => __awaiter(this, void 0, void 0, function* () {
    try {
        yield connection.start().then(function () {
            connection.invoke("ConnectKubeAsync", "gwc-22z3vm7zd2oo").catch(function (err) {
                return console.error(err.toString());
            });
           
        }).catch(function (err) {
            return console.error(err.toString());
        })
    }
    catch (e) {
        console.error(e.toString());
    }
}))();