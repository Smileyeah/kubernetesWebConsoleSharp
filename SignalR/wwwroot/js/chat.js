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

document.getElementById("streamButton").addEventListener("click", (event) => __awaiter(this, void 0, void 0, function* () {
    try {
        connection.stream("Counter")
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

    // console.log(event.key)
    // console.log(event.target.value)
    // if((event.keyCode > 64 && event.keyCode < 91) 
    //     || (event.keyCode > 47 && event.keyCode < 58) 
    //     || (event.keyCode > 185 && event.keyCode < 193)
    //     || (event.keyCode > 218 && event.keyCode < 223)
    //     || validKeyCode.includes(event.keyCode)){

        // console.log(event.keyCode)
        // console.log(event.key === "Enter")
        
        if(event.key === "Enter"){
            const subject = new signalR.Subject();
            yield connection.send("UploadStream", subject);
            subject.next(event.target.value + '\n');
            document.getElementById("messageInput").value = '';
        }
    // }
}));

// We need an async function in order to use await, but we want this code to run immediately,
// so we use an "immediately-executed async function"
(() => __awaiter(this, void 0, void 0, function* () {
    try {
        yield connection.start().then(function () {
            connection.invoke("ConnectKubeAsync", "gwc-zhz9i8alt0c7").catch(function (err) {
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