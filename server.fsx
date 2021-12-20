open System
open System.Security.Cryptography
open System.Diagnostics

#r "nuget: Akka.FSharp" 
#r "nuget: Akka.Remote"
open Akka.Actor
open Akka.FSharp
open Akka.Configuration


(*
The function generateHashFromString takes an input as a string which then generates an unique hash value. 
This hash value is then converted into a string and returned.
*)
let generateHashFromString = fun (inputString: string) ->
    let mutable sb = ""
    let sha256Key = SHA256Managed.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(inputString))
    for c in sha256Key do
        sb <- sb + c.ToString("X2")
    sb

(*
These are helping variables that are used in later functionalities of the Server.
*)
let run = 1 
let mutable totalStrings = 80000000 // This is the total number of strings generated that will be used to find bitcoins
let mutable no_Client = 0 // By default the number of clients conencted is zero
let mutable clientList =[] // If client.fsx is running, this list will be populated with respective clients.
let lastString = generateHashFromString "akhatri" 
let leading0s = fsi.CommandLineArgs.[1] |> int // Input from command-line which is number of leading zeroes.
let gatorid = "architkhatri;" // gatorid
let totalProcessors = Environment.ProcessorCount // The total number of CPU in the machine
let stringsPerProcessor = (totalStrings - run)/(totalProcessors) // The workload given to each processor
let mutable bitcoinCount = 0
    

(*
This function takes input Hash String and The required number of leading zeroes and returns true if a valid coin.
*)
let isValidBitcoin (inputString: string) (reqdZeroes: int) : Boolean = 
    let sb = generateHashFromString(inputString)
    let mutable zeroesFound = true
    let mutable count = 0

    for i = 0 to (sb.Length-1) do
        if sb.Chars(i) = '0' && zeroesFound 
        then count <- count+1
        else zeroesFound <- false
    (count >= reqdZeroes)

type randomStringObj = string*string*int*int*int 

type Worker(name) =
    inherit Actor()
        override this.OnReceive message = 
            match message with
            
            |  :? randomStringObj as message ->
                let (currHash, gatorid, level, start, stop) = unbox<string*string*int*int*int> message
                let mutable itr = start
                while itr <= stop do
                    let generatedBitcoin = gatorid + currHash + itr.ToString()
                    itr <- itr+1
                    if isValidBitcoin generatedBitcoin level then 
                        bitcoinCount <- bitcoinCount + 1
                        let generatedHash = generateHashFromString generatedBitcoin
                        printfn "Generated BTC: %s , HashCode: %s " generatedBitcoin generatedHash

// The default configuration of the server file as required in the AKKA
let configuration = 
    ConfigurationFactory.ParseString(
        @"akka {
            actor {
                provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
                debug : {
                    receive : on
                    autoreceive : on
                    lifecycle : on
                    event-stream : on
                    unhandled : on
                }
            }
            remote {
                helios.tcp {
                    port = 8080
                    hostname = 127.0.0.1
                }
            }
        }")

// Creates Actor
let system = ActorSystem.Create("RemoteFSharp", configuration)

type Connection = string*int*list<IActorRef>
type outputStr = string*string

// Actor Reference 
let leader = 
    spawn system "server"
    <| fun ip_op ->
        let rec repeat() =             
            actor {
                let! receivedMsg = ip_op.Receive()
                let deliverMsg = ip_op.Sender()

                // This is the message received from the client which is then processed
                match box receivedMsg with
                | :? randomStringObj as message ->
                    let (currHash, gatorid, level, start, stop) = unbox<string*string*int*int*int> message
                    let stringsToGenerate = (stop - start)/(totalProcessors)
                    for i=1 to totalProcessors do
                        system.ActorOf(Props(typedefof<Worker>, [| string(i) :> obj |])) <! (currHash, gatorid, level, (i-1)*stringsToGenerate+1, i*stringsToGenerate)
                
                | :? Connection as message -> 
                    let (str,countClient, listClients) = unbox<Connection> message
                    no_Client <- countClient
                    clientList <- clientList |> List.append listClients
                    let temp = totalStrings
                    totalStrings <- totalStrings + stringsPerProcessor*no_Client
                    if no_Client > 0 then
                        for i=1 to no_Client do
                            let hashFromClient = lastString + (temp |> string)
                            clientList.Item(i-1) <! (hashFromClient, gatorid, leading0s, (i-1)*stringsPerProcessor+1, i*stringsPerProcessor)
                
                // Handler for successfully mined bitcoins on client machines.
                | :? outputStr as message ->
                    let (generatedBTC, BTC_hash) = unbox<string*string> message
                    printfn "Generated BTC: %s , Hashcode: %s " generatedBTC BTC_hash    
                return! repeat() 
            }
        repeat()

let processCurrent = Process.GetCurrentProcess() // Get the process id
let cpuTimeStamp = processCurrent.TotalProcessorTime // Gets the current time in CPU clock
let timer= Stopwatch() //variable to calculate time
timer.Start() // starts the timer

leader <! (lastString, gatorid, leading0s, run, totalStrings)

system.Terminate()
system.WhenTerminated.Wait()
printfn "Total Number of Bitcoins found with user provided leading-zeroes %d" bitcoinCount
let cpuTime = (processCurrent.TotalProcessorTime-cpuTimeStamp).TotalMilliseconds
printfn "CPU time = %dms" (int64 cpuTime)
printfn "Absolute time = %dms" timer.ElapsedMilliseconds
let ratio = float (int64 cpuTime)/ float timer.ElapsedMilliseconds
printfn "ratio: %f"  ratio
0