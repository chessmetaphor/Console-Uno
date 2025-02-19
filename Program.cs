
using System.Text;

namespace Uno_Game {

    /* 
       If the first card that palys is a Draw 4 or Wild...
       > Check to see if the CPUs still pick a non-RGBY color. They shouldn't, ever.
       > So far its been 2 Reds
    */

     sealed class UNO_Game {

        #region Cards & Players

        public enum Suit { Red, Blue, Green, Yellow, Black, Unassigned }

        public enum Kind { Normal, Skip, Reverse, Draw_2, Wild, Draw_4 }

         /// <summary>
        /// All cards are objects with an assigned number, color, and effect.
        /// </summary>
        struct Card(Suit suit, Kind effect, int number) {
            public int number = number;
            public Suit suit = suit;
            public Kind effect = effect;
        }

        /// <summary>
        /// Every player has a name, and a marker for whether they are a player or CPU.
        /// </summary>
        /// <param name="name">The name of whoever's playing.</param>
        /// <param name="tp">The marker for whether a player is you or a CPU.</param>
        struct Player(string name, Player.Type tp) {
            public string name = name;
            public Type type = tp;

            public enum Type { YOU, CPU }
        } 

        #endregion

        // =====================================================================================

        #region Global Variables

        // Keeps track of whose turn it is
        static int playerIndex; 
        static bool reverseOrder = false;


        // The color and number of the last played card.
        static Suit currentColor = Suit.Unassigned;
        static int currentNumber = -9;


        // Where to remove the last played card.
        static int listIndex; // from whose hand
        static int removeIndex; // spot in the hand the card occupies 


        // Keeps track of the turns where a player couldn't do anything.
        static bool voidTurn = false; // a turn where a player can play no cards or draw from the deck is considered void
        static int stalled = 0; // count of how many void turns occurred in a row
        static bool highlight = false; // Marks cards that you can play green when you pick a wrong one.


        // Keeps track of the status of the draw pile and all players.
        static Stack<Card> drawPile = []; // where every player pulls new cards from
        static Dictionary<Player, List<Card>> allPlayers = []; // every player and their cards
        
        #endregion

        // =====================================================================================

        #region Gameplay Tasks

            // =============================================================================
            
            #region Game Initializer Methods 

            /// <summary>
            /// Sets the global variables back to their default values.
            /// Clears every existing player and every existing card in the game from the draw pile.
            /// This is all done before the start of a brand new game.
            /// </summary>
            static void ResetGame() {
                reverseOrder = false;
                currentColor = Suit.Unassigned;
                currentNumber = -9;
                voidTurn = false;
                stalled = 0;
                highlight = false;
                drawPile.Clear();
                allPlayers.Clear();
            }

            /// <summary>
            /// Creates all the players so the game can start.
            /// </summary>
            /// <returns></returns>
            async static Task CreatePlayers() {
                int playerNum = 0; 
                do {
                    // Give the number of CPUs you want to play against.

                    Console.WriteLine("How many are playing? (Answer 2 - 10.)");
                    string nm = Console.ReadLine();

                    if(int.TryParse(nm, out int ans)) {
                        playerNum = Math.Clamp(ans, 2, 10);

                        Console.WriteLine($"Generating {playerNum} players and starting the game.");


                        // Start building the dictionary that stores all players and their deck of cards.

                        allPlayers.Add(new Player("You", Player.Type.YOU), []);

                        for(int p = 1; p < playerNum; p++) { 
                            string name = $"Player {p + 1}";
                            allPlayers.Add(new Player(name, Player.Type.CPU), []); 
                            Console.WriteLine($"{name} joined you.");
                        }   
                    }
                    else {
                        Console.WriteLine("Just give an answer 2 through 10.");
                        await Task.Delay(1000);
                    }

                } while (playerNum == 0);

                await Task.Delay(3000);
            }

            /// <summary>
            /// Fills out each player's deck at the start of the game.
            /// </summary>
            /// <returns></returns> 
            async static Task CardDistribution() {
                List<Card> builder = [];
                Random rnd = new();

                foreach(Suit suit in Enum.GetValues(typeof(Suit))) {
                    if (suit == Suit.Black) break; // We only need this loop for the first four colors.

                    // Create a wild card and a draw 4 card. Since this foreach loop runs four times, there will be four in total for both kinds.
                    builder.Add(new Card(Suit.Black, Kind.Wild, -5));
                    builder.Add(new Card(Suit.Black, Kind.Draw_4, -4));

                    // A for loop that runs twice. Out of it, we get two skips, reverses, and draw 2s, AND two sets of numbered cards 1 thorugh 10.
                    for(int d = 0; d < 2; d++) {
                        builder.Add(new Card(suit, Kind.Skip, -3));
                        builder.Add(new Card(suit, Kind.Reverse, -2));
                        builder.Add(new Card(suit, Kind.Draw_2, -1));

                        // For loop for numbered cards, 1 through 10.
                        for(int n = 1; n < 10; n++) builder.Add(new Card(suit, Kind.Normal, n));
                    }

                    // Finally, every RGBY suit has one 0 card.
                    builder.Add(new Card(suit, Kind.Normal, 0));
                }

                await Task.Delay(1000);


                // Shuffle all the cards.
                builder = [..builder.OrderBy(_=> rnd.Next())];

                // Push every card we created to the draw pile.
                foreach (Card card in builder) drawPile.Push(card);


                await Task.Delay(1000);

                // Finally, give the players their cards.
                foreach(List<Card> decks in allPlayers.Values)
                    for(int a = 0; a < 7; a++) decks.Add(drawPile.Pop());
            }

            #endregion

            // =============================================================================

            #region Action Methods

            /// <summary>
            /// Check to see if a card can be played at all this turn.
            /// </summary>
            /// <param name="toPlay">Pass any card from any deck to see if it can be played this turn. </param>
            /// <returns>True: It CAN be played. False: It CAN'T be played.</returns>
            static bool EvaluateCard(Card toPlay) => toPlay.suit.Equals(currentColor) || toPlay.number == currentNumber;

            /// <summary>
            /// Quick way to grab the current player from the dictionary.
            /// </summary>
            /// <returns>The player whose turn it is.</returns>
            static Player GetPlayer() => allPlayers.ElementAt(playerIndex).Key;

            /// <summary>
            /// Quick way to grab the current player's deck from the dictionary.
            /// </summary>
            /// <returns>A list containing all the cards in the current player's deck.</returns>
            static List<Card> GetDeck() => allPlayers.ElementAt(playerIndex).Value;

            /// <summary>
            /// Returns what color card the current CPU has the most of.
            /// </summary>
            /// <returns></returns>
            static Suit RecommendColor() {
                Dictionary<Suit, int> amounts = new() {
                        {Suit.Red, 0},
                        {Suit.Blue, 0},
                        {Suit.Green, 0},
                        {Suit.Yellow, 0}
                };

                foreach(Card card in GetDeck().Where(d => !d.suit.Equals(Suit.Black))) amounts[card.suit] += 1;
                                
                return amounts.OrderByDescending(x => x.Value).ToList().First().Key;
            }

            // =========================================

            /// <summary>
            /// The global player index is incremented or decremented based on the players' order.
            /// </summary>  
            static void UpdatePlayerIndex() {
                
                playerIndex = !reverseOrder ? playerIndex + 1 : playerIndex - 1; 

                if(playerIndex >= allPlayers.Count) {
                    playerIndex = 0;
                }
                else if(playerIndex < 0) playerIndex = allPlayers.Count - 1;
            }

            /// <summary>
            /// Add a new card from the shared deck to the current player's hand.
            /// </summary>
            static void AddCard() {
                if(drawPile.Count > 0) {
                    GetDeck().Add(drawPile.Pop());
                    if(GetPlayer().type == Player.Type.CPU) Console.WriteLine($"\n>> {GetPlayer().name} drew a card. Their card count is now {GetDeck().Count}.");
                }
                else {
                    Console.WriteLine("\n>> There are no more cards that can be pulled...");
                }
            }

            /// <summary>
            /// Perform a different action depending on the card that's being played. 
            /// </summary>
            /// <param name="playThis">The card to be played from the current player's deck</param>
            async static Task PlayCard(Card playThis) {
                // Start building the message to end off the current turn.

                if(GetDeck().Count == 2) Console.WriteLine($"\n>> {GetPlayer().name} {(GetPlayer().type == Player.Type.YOU ? "have" : "has")} UNO!");

                Console.Write($"\n>> {GetPlayer().name} ({GetDeck().Count - 1}): ");


                // Change the current color and number to the card that was played then moved on to the next player.

                listIndex = playerIndex;
                removeIndex = GetDeck().IndexOf(playThis);

                if(!playThis.suit.Equals(Suit.Black)) currentColor = playThis.suit;

                currentNumber = playThis.number;

                if(playThis.effect == Kind.Reverse) reverseOrder = !reverseOrder;

                UpdatePlayerIndex();


                // Perform the last card's effect on this player.

                switch(playThis.effect) {
                    
                    case Kind.Skip:
                        Console.WriteLine($"A {Enum.GetName(currentColor)} skip card?! {(GetPlayer().type == Player.Type.YOU ? "Your" : GetPlayer().name + "'s")} turn was skipped!");
                        UpdatePlayerIndex();
                    break;

                    case Kind.Reverse:
                        Console.WriteLine($"A {Enum.GetName(currentColor)} Reverse card?! It's {(GetPlayer().type == Player.Type.YOU ? "your" : GetPlayer().name + "'s")} turn!");
                    break;

                    case Kind.Draw_2:
                        Console.WriteLine($"{currentColor} Draw 2! {GetPlayer().name} {(GetPlayer().type == Player.Type.YOU ? "were" : "was")} forced to draw two cards!");

                        for(int d = 0; d < Math.Clamp(drawPile.Count, 1, 2); d++) AddCard();
                        
                        UpdatePlayerIndex();
                    break;

                    case Kind.Draw_4:
                        Console.WriteLine($"{GetPlayer().name} {(GetPlayer().type == Player.Type.YOU ? "were" : "was")} forced to draw FOUR cards! The new color is {Enum.GetName(currentColor)}.");
                        
                        for(int d = 0; d < Math.Clamp(drawPile.Count, 1, 4); d++) AddCard();

                        UpdatePlayerIndex();
                    break;

                    case Kind.Wild:
                        Console.WriteLine($"A wildcard was just played?! The new color is {Enum.GetName(currentColor)}.");
                    break;

                    case Kind.Normal:
                        Console.WriteLine($"A {Enum.GetName(currentColor)} {currentNumber} card was played.");
                    break;
                }
            
                await Task.Delay(0);
            }

            #endregion

            // =============================================================================

            #region Turn Methods

            /// <summary>
            /// Draws your cards to the console.
            /// </summary>
            static void ViewCards() {
                Console.WriteLine("Here's your cards.");
                bool hl;
                int ind = 1;
                
                foreach(Card card in GetDeck()) {

                    hl = highlight && ( card.suit == Suit.Black || EvaluateCard(card) );

                    if (hl) Console.ForegroundColor = ConsoleColor.Green;

                    string display = $" || ({ind}) { card.effect switch {
                        Kind.Normal => $"{Enum.GetName(card.suit)} {card.number}",
                        Kind.Skip => $"{Enum.GetName(card.suit)} Skip",
                        Kind.Reverse => $"{Enum.GetName(card.suit)} Reverse",
                        Kind.Draw_2 => $"{Enum.GetName(card.suit)} Draw 2",
                        Kind.Draw_4 => "Draw 4",
                        Kind.Wild => "Wild",
                        _ => ""
                    }} || ";

                    Console.Write(display);

                    if (hl) Console.ResetColor();

                    ind++;
                }

                Console.Write($"\n{ (highlight ? "" : "What will you do? ") }");
            }

            /// <summary>
            /// The instructions for your turn. The game will wait for you to choose a valid card, and a new color when you play a wild card.
            /// </summary>
            /// <returns></returns>
            async static Task YourTurn() {
                ViewCards(); 

                bool canPlay = false;
                bool earlyBreak = false;
                int cardIndex = 0;

                /* While you don't have a card to play:
                   The program will wait for your input. You can type in ADD for a new card. END to end your turn,
                   or the number of the card you want to play.
                */
                do {
                   string input = Console.ReadLine()?.ToUpper();
                   
                   switch(input) {
                        case "ADD":
                            AddCard();
                            Console.WriteLine($"You drew a card. Your total is {GetDeck().Count}.");
                            
                            await Task.Delay(1500);

                            ViewCards();
                        break;

                        case "QUICKADD":
                            int count = 0;
                            while(drawPile.Count > 0) {
                                AddCard();
                                count++;
                                if(GetDeck().Any(EvaluateCard) || GetDeck().Any(s => s.suit == Suit.Black)) break;
                            }

                            if(count > 0) Console.WriteLine($"You pulled {count} more card{(count == 1 ? "." : "s.")}");

                            await Task.Delay(1500);

                            ViewCards();
                        break;

                        case "END":
                            earlyBreak = true;
                            voidTurn = true;
                        break;

                        default:
                            if(int.TryParse(input, out int num)) {
                                // Evaluate the card that was chosen to see if it can be played.

                                int index = Math.Clamp(num - 1, 0, GetDeck().Count - 1);

                                if(GetDeck()[index].suit != Suit.Black) {
                                    if(EvaluateCard(GetDeck()[index])) { 
                                        cardIndex = index;
                                        canPlay = true;
                                    }
                                    else { 
                                        Console.WriteLine("Can't play that one; doesn't match either the last card's color or number.\n"); 
                                        await Task.Delay(1000);

                                        if(!highlight) highlight = true;
                                        ViewCards();
                                    }
                                }
                                else {
                                    // If you are getting ready to play a wild card...
                                    if(GetDeck().Count > 1) {
                                        Console.Write("You chose a wild card! What color card should the next player put down? ");

                                        // The program will wait for you to input a RGBY color.

                                        while(true) {
                                            string c_input = Console.ReadLine();

                                            if(Enum.TryParse(c_input, out Suit result)) {
                                                if(!result.Equals(Suit.Black) && !result.Equals(Suit.Unassigned)) {
                                                    currentColor = result;
                                                    break;
                                                }
                                                else Console.WriteLine("That can't be used. Choose Red, Blue, Yellow, or Green (Case-sensitive). ");
                                            }
                                            else Console.WriteLine("Invalid input. Choose Red, Blue, Yellow, or Green (Case-sensitive). ");
                                        }
                                    }
                                    else {
                                        Console.ForegroundColor = ConsoleColor.Yellow;
                                        Console.WriteLine("You're finishing things off with a wildcard!");
                                        Console.ResetColor();
                                        currentColor = Suit.Unassigned;
                                    }

                                    cardIndex = index;
                                    canPlay = true;
                                }
                            }
                            else Console.WriteLine("Invalid input. Try typing 'ADD' for a new card, the number next to a card you might want to play, or 'END' to skip your turn. ");
                            
                        break;
                   }

                   if(earlyBreak) break; 

                } while(!canPlay); 

                if(canPlay) await PlayCard(GetDeck()[cardIndex]);
                else UpdatePlayerIndex();

                await Task.Delay(2000);
            }

            /// <summary>
            /// The behavior of the CPU player(s). They'll pick a card that matches the last card's number or color if they have one, and they'll play a 
            /// wildcard if they don't. If they have no cards they can play, they'll pull from the deck until there are no cards to pull, or they eventually pull one
            /// they can play.
            /// </summary>
            /// <returns></returns>
            async static Task CPUTurn() {
                bool playCard = false;
                int cardIndex = 0;
                Random rnd = new();

                /* Checks:
                    If they have any normal cards match the color or number of the last played card with EvaluateCard(). If they do, play any card that matches.

                    Check if there is any wild cards. If there are, play either a wild or draw 4 at random, 
                    after changing the current color to what the CPU has the most of.

                    If the CPU has no wildcards or normal cards they can play, check if there are any cards still in the shared deck. Just call addcard().

                    If there's no more cards they can pull, there's literally nothing the CPU can do. Their turn is void, so the do-while loop breaks.
                */
                
                do {
                    if(GetDeck().Any(EvaluateCard)) {
                        // Picks a random card out of every one of them that can be played on this turn.
                        List<int> eval = [];

                        foreach(Card crd in GetDeck().Where(EvaluateCard)) eval.Add(GetDeck().IndexOf(crd));
                        
                        cardIndex = eval[rnd.Next(eval.Count)];

                        // If a wildcard was picked, then the recommended color is chosen.
                        currentColor = RecommendColor();

                        playCard = true;
                    }
                    else if(GetDeck().Any(n => n.suit == Suit.Black)) {
                    
                        // Gather all the wildcards in the deck if there are no numbered cards that can be played.
                        var eval = GetDeck().Where(e => e.suit == Suit.Black);

                        // If there are ONLY wild cards, a random one is grabbed from the deck and a random RGBY color is picked.

                        if(GetDeck().Count - eval.Count() == 0) {
                            // If that wildcard was their last card, there's no need to assign a color at all.

                            if(GetDeck().Count == 1)
                                currentColor = Suit.Unassigned;
                            else {
                                Random rd = new();
                                int choice = rd.Next(4);
                                currentColor = (Suit)choice;
                            }
                           
                            cardIndex = rnd.Next(GetDeck().Count);
                            playCard = true;
                        }
                        else {
                            
                            // A random wildcard will be grabbed from the deck after determining the color the CPU has the most of.
                           currentColor = RecommendColor();

                           cardIndex = GetDeck().IndexOf(eval.ElementAt(rnd.Next(eval.Count())));
                            
                           playCard = true;
                        }
                    }
                    else if(drawPile.Count > 0) {
                        // If there are no wildcards at all and no numbered cards that can be played, the CPU draws a card from the deck.

                        AddCard();
                    }
                    else {
                        // If there are no playable cards AND the draw pile is empty, their turn is considered void.

                        Console.WriteLine($"\n>> {GetPlayer().name} can't play at all this turn.");
                        voidTurn = true;
                        break;
                    }

                } while(!playCard);

                if(playCard) await PlayCard(GetDeck()[cardIndex]);
                else UpdatePlayerIndex();
                
                await Task.Delay(4000);
            }

            #endregion

        // =============================================================================

        #endregion

        // =====================================================================================

        async static Task Main() {
            int wins = 0;
            bool noMore = false;

            do {

                #region Create

                // Create players.

                await Task.Run(CreatePlayers);

                // Decide whose going first: you or a CPU.

                string answer = string.Empty;
        
                do {
                    Console.WriteLine("\nAre you dealing? (Y/N)");

                    string choose = Console.ReadLine();

                    switch(choose) {
                        case "Y":
                            answer = "!";
                            playerIndex = 0;
                        break;

                        case "N":
                            Random rnd = new();
                            answer = "!";
                            playerIndex = rnd.Next(1, allPlayers.Count);
                            break;

                        default:
                            Console.WriteLine("Just answer the question. (Y/N)");
                            break;
                    }

                } while(answer == string.Empty);

                answer = string.Empty;

                #endregion

                #region Deal

                // The player's hands are filled with 7 cards at the start of the game.

                Console.WriteLine("Distributing cards. Please wait...");

                await Task.Run(CardDistribution);

                // Start the discard pile with the card at the top of the draw pile.

                Card firstCard = drawPile.Pop();
                currentColor = firstCard.suit;
                currentNumber = firstCard.number;

                // Describe what just entered the discard pile. The underscores in Draw_2 and 4 should be gone
                string desc = $">> {firstCard.suit switch {
            
                    Suit.Black => $"{Enum.GetName(typeof(Kind), firstCard.effect)}",
                    _ => $"{currentColor} {(firstCard.effect == Kind.Normal ? currentNumber : Enum.GetName(typeof(Kind), firstCard.effect))}" 
                    }

                } added to the discard pile.";

                Console.WriteLine(desc.Replace('_',' '));
                
                await Task.Delay(900);


                Console.WriteLine("Let's start the game...");

                await Task.Delay(1500);

                #endregion
            
                #region Play

                // The game runs as long as every player still has cards.

                while(!allPlayers.Any(pl => pl.Value.Count == 0) && stalled < allPlayers.Count) {
                    
                    if(GetPlayer().type == Player.Type.CPU) await CPUTurn();
                    else await YourTurn(); 

                    // If you or a CPU did nothing this turn, no card is removed from your hands.
                    if(!voidTurn) { 
                        allPlayers.ElementAt(listIndex).Value.RemoveAt(removeIndex);
                        if(stalled > 0) stalled = 0;
                    }
                    else { 
                        voidTurn = false;
                        stalled++;
                    }

                    if (highlight) highlight = false;
                }

                if(stalled < allPlayers.Count) {
                    // When the loop ends, the player with no cards is announced as the winner, then this game is over.

                    var winner = allPlayers.First(pl => pl.Value.Count == 0);
                
                
                    Console.WriteLine($"\n ~~~ GAME OVER! {winner.Key.name} won. ~~~ ");

                    if(winner.Key.type == Player.Type.YOU) wins++;
                }
                else {
                    // If too many void turns have been taken in a row, the game ends in a draw.
                    
                    Console.WriteLine($"\nSince no one can play their cards, the game has to end in a {(allPlayers.Count > 2 ? $" {allPlayers.Count}-way" : " ")} draw.");
                }

                #endregion

                #region Finish

                // Give the player the option to play again if they want. 

                do {
                    Console.WriteLine("\nDo you want to play again? (Y/N)");

                    string r_answer = Console.ReadLine();

                    switch(r_answer) {
                        case "Y":
                            Console.WriteLine("Starting another round.");
                            ResetGame();
                            answer = "!";
                            break;
                        case "N":
                            noMore = true;
                            answer = "!";
                            break;
                        default:
                            Console.WriteLine("Answer the question. (Y/N)");
                            break;
                    }

                } while (answer == string.Empty);

                #endregion

            } while(!noMore);

            Console.WriteLine($"\n ~~~ Game complete! You won {wins} {(wins == 1? "time" : "times")}. ~~~");
        }
    } 

}