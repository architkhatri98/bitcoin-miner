open System
open System.Security.Cryptography

#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote"
open Akka.Actor
open Akka.FSharp
open Akka.Configuration

let serverAddress = fsi.CommandLineArgs.[1] |> string // User specified Server Address
let finalAddress = "akka.tcp://RemoteFSharp@" + serverAddress + ":8080/user/server" // The original Server Address with port
let totalProcessors = Environment.ProcessorCount // Total Number of processes in Client machine


// Function that takes input a string and generates a hash-value for it and returns it as a string
let generateHashFromString = fun (inputString: string) -> 
    let mutable sb = ""
    let sha256Key = SHA256Managed.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(inputString))
    for c in sha256Key do
        sb <- sb + c.ToString("X2")
    sb
   
// Checks if the Hash-value generated has the required number of leading zeroes
let isValidBitcoin (inputString: string) (reqdZeroes: int) : Boolean = 
    let sb = generateHashFromString(inputString)
    let mutable zeroesFound = true
    let mutable count = 0

    for i = 0 to (sb.Length-1) do
        if sb.Chars(i) = '0' && zeroesFound 
        then count <- count+1
        else zeroesFound <- false
    (count >= reqdZeroes)



// Configuration file for the AKKA to connect with the server
let configuration = 
    ConfigurationFactory.ParseString(
        @"akka {
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
                
            }
            remote {
                helios.tcp {
                    port = 8081
                    hostname = 127.0.0.1
                }
            }
        }")

let system = ActorSystem.Create("ClientFsharp", configuration) // system for the client

type randomStringObj = string*string*int*int*int
type Worker(name) =
    inherit Actor()
        override this.OnReceive message = 
            match message with
            | :? randomStringObj as message ->
                let (currHash, gatorid, level, start, stop) = unbox<randomStringObj> message
                for i = start to stop do
                    let generatedBitcoin = gatorid + currHash + i.ToString()
                    if isValidBitcoin generatedBitcoin level then 
                        let generatedHash = generateHashFromString generatedBitcoin
                        //printfn "%s %s " generatedBitcoin generatedHash
                        this.Sender <! (generatedBitcoin, generatedHash)
                        
            | _ -> failwith "error encountered..."




type outputStr = string*string
type Connection = string*int*List<IActorRef>

// Actor Reference
let Leader = 
    spawn system "client"
    <| fun ip_op ->
        let rec repeat() =
            actor {
                let! msg = ip_op.Receive()
                let sender = ip_op.Sender()
                let echoServer = system.ActorSelection(finalAddress)
                match box msg with
                | :? Connection as message -> 
                    echoServer.Tell(message)     
                | :? outputStr as message ->
                    printfn "bitcoin mined: %A" message
                    echoServer.Tell(message)
                | _ -> failwith "error encountered..."
                return! repeat() 
            }
        repeat()

let mutable clientList = [] // List of Actor References or clients
for i in 1..totalProcessors do
    clientList <- List.append clientList [system.ActorOf(Props(typedefof<Worker>, [| string(id) :> obj |]))]

Leader <! ("worker", totalProcessors, clientList)

system.Terminate()
system.WhenTerminated.Wait()