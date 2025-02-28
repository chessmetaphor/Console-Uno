namespace Uno_Game {

    /*
        There's some rules left to incorporate!
        - There's new cards that let you swap decks with other players holy wow
        Gotta give you the choice to include Swap and Shuffle Hands next
        - I went my whole life not knowing uno had a score system lol
    */

     sealed class UNO_Game {

        #region Cards & Players

        public enum Suit { Red, Blue, Green, Yellow, Black, Unassigned }

        public enum Kind { Numbered, Skip, Reverse, Draw_2, Wild, Draw_4 }

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
        static bool highlight = false; // Marks cards that you can play green when you pick a wrong one.


        // Where to remove the last played card.
        static int listIndex; // from whose hand
        static int removeIndex; // spot in the hand the card occupies 
        
       
        // Keeps track of the status of the draw pile and all players.
        static readonly Stack<Card> drawPile = []; // where every player pulls new cards from
        static readonly Stack<Card> discardPile = []; // where all cards that were played go
        private static List<Card> builder = []; // temporary list for cards that are being reshuffled

        static readonly Dictionary<Player, List<Card>> allPlayers = []; // every player and their cards
        
        #endregion

        // =====================================================================================

        #region All Methods

            // =============================================================================
            
            #region Game Creation 

            /// <summary>
            /// Fills out each player's deck at the start of the game.
            /// </summary>
            /// <returns></returns> 
            async static Task CreateCards() {
                builder.Clear();

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
                        for(int n = 1; n < 10; n++) builder.Add(new Card(suit, Kind.Numbered, n));
                    }

                    // Finally, every RGBY suit has one 0 card.
                    builder.Add(new Card(suit, Kind.Numbered, 0));
                }

                await Task.Run(RebuildDrawPile);
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


            /* Before the game can start, something needs to happen depending on the card's effect. 
                    If a Draw 4 was discarded, place it back in the draw pile and reshuffle it. Do this until a draw 4 does NOT appear again.
                    (DONE!)

                    If it was a skip card, update the player index. (DONE!)
                    If it was a reverse card, set reverseOrder to true. (DONE!)
                    If its a Draw 2, Pop() two cards from the draw pile into the current player's deck then update the player index. (DONE!)
                    If its a wildcard, the current color is set with RecommendColor(). (DONE!)

                    You'll have to swap the decks and cards among specific players when you implement the new cards too.
                */
            async static Task RebuildDiscardPile() {
                // Take the card at the top of the draw pile and discard it.
                discardPile.Push(drawPile.Pop());

                // If a Draw 4 was discarded, add it back into the deck and reshuffle.

                while(discardPile.Peek().effect == Kind.Draw_4) {
                    
                    Console.WriteLine("\n>> A Draw 4 was discarded. Reshuffling...");

                    builder.Clear();
                    drawPile.Push(discardPile.Pop());

                    while(drawPile.Count > 0) builder.Add(drawPile.Pop());

                    await Task.Run(RebuildDrawPile);

                    discardPile.Push(drawPile.Pop());
                }

                Card firstCard = discardPile.Peek();
                if (firstCard.effect != Kind.Wild) currentColor = firstCard.suit;
                currentNumber = firstCard.number;


                // Any cards discarded first (and after a draw 4 and reshuffle) affects the first player.

                switch(firstCard.effect) {

                    case Kind.Skip:
                        UpdatePlayerIndex();
                    break;

                    case Kind.Reverse:
                        reverseOrder = true;
                        UpdatePlayerIndex();
                    break;

                    case Kind.Draw_2:
                        for(int d = 0; d < 2; d++) AddCard();
                        UpdatePlayerIndex();
                    break;

                    case Kind.Wild:
                        currentColor = RecommendColor();
                    break;
                }


                // Describe what just entered the discard pile.
                
                string desc = $">> {firstCard.suit switch {
                
                    Suit.Black => $"{Enum.GetName(typeof(Kind), firstCard.effect)}",
                    _ => $"{currentColor} {(firstCard.effect == Kind.Numbered ? currentNumber : Enum.GetName(typeof(Kind), firstCard.effect))}" 
                    }

                } added to the discard pile.";

                Console.WriteLine(desc.Replace('_',' ') + $"{ (firstCard.effect == Kind.Wild ? $" The first color is {currentColor}." : string.Empty) }" );


                await Task.Delay(0);
            }
            
            async static Task RebuildDrawPile() {
                if(builder.Count == 0) Console.WriteLine("There are no cards to reshuffle.");
                else {
                    Random rnd = new();

                    // Shuffle all the cards.
                    builder = [..builder.OrderBy(_=> rnd.Next())];

                    // Push every discarded card back to the draw pile.
                    foreach (Card card in builder) drawPile.Push(card);

                    builder.Clear();
                }

                await Task.Delay(900);
            }

            
            /// <summary>
            /// Sets the global variables back to their default values.
            /// Clears every existing player and every existing card in the game from the draw and discard piles.
            /// This is all done before the start of a brand new game.
            /// </summary>
            static void ResetGame() {
                reverseOrder = false;
                currentColor = Suit.Unassigned;
                currentNumber = -9;
                highlight = false;
                drawPile.Clear();
                discardPile.Clear();
                allPlayers.Clear();
            }

            #endregion

            // =============================================================================

            #region Gameplay Flow 

            /// <summary>
            /// Reshuffles the cards from the discard pile and adds them back into the draw pile.
            /// </summary>
            /// <returns></returns>
            async static Task EmptyDiscardPile() {
                while(discardPile.Count > 0) builder.Add(discardPile.Pop());

                await Task.Run(RebuildDrawPile);
            }

            /// <summary>
            /// Add a new card from the draw pile to the current player's hand.
            /// </summary>
            static void AddCard() {
                if(drawPile.Count > 0) 
                    GetDeck().Add(drawPile.Pop());
                else 
                    Console.WriteLine("\n>> There are no more cards that can be pulled...");
            }

             /// <summary>
            /// Check to see if a card can be played at all this turn.
            /// </summary>
            /// <param name="toPlay">Pass any card from any deck to see if it can be played this turn. </param>
            /// <returns>True: It CAN be played. False: It CAN'T be played.</returns>
            static bool EvaluateCard(Card toPlay) => toPlay.suit.Equals(currentColor) || toPlay.number == currentNumber;

            /// <summary>
            /// Called at the start of every turn. If you or a CPU can't play, the draw pile gets rebuilt so new cards can be pulled and the game continues.
            /// </summary>
            /// <returns></returns>
            async static Task EvaluateTurn() {
                if(!GetDeck().Any(e => e.suit == Suit.Black || EvaluateCard(e)) && drawPile.Count == 0){
                    Console.WriteLine("\n>> The draw pile is empty. Reshuffling...");
                    await Task.Run(EmptyDiscardPile);
                    if(drawPile.Count > 0) AddCard();
                }
            }

            /// <summary>
            /// 
            /// </summary>
            /// <returns>The deck of the current player.</returns>
            static List<Card> GetDeck() => allPlayers.ElementAt(playerIndex).Value;

            /// <summary>
            /// Looks through the dictionary of all players and returns the deck of the player at one at the index provided.
            /// </summary>
            /// <param name="which">The index of the player the deck belongs to</param>
            /// <returns>The deck of the player at this index.</returns>
            static List<Card> GetDeck(int which) => allPlayers.ElementAt(which).Value;

            /// <summary>
            /// 
            /// </summary>
            /// <returns>The player whose turn it is.</returns>
            static Player GetPlayer() => allPlayers.ElementAt(playerIndex).Key;

            /// <summary>
            /// In the list of all players that exist in the game, this is the index of the one whose going next.
            /// </summary>
            /// <returns></returns>
            static int NextPlayerIndex() {
                int p = !reverseOrder ? playerIndex + 1 : playerIndex - 1; 

                if(p >= allPlayers.Count) return 0;
                else if(p < 0) return allPlayers.Count - 1; 

                return p;
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
                        Console.WriteLine($"A {Enum.GetName(currentColor)} skip card! {(GetPlayer().type == Player.Type.YOU ? "Your" : GetPlayer().name + "'s")} turn was skipped.");
                        UpdatePlayerIndex();
                    break;

                    case Kind.Reverse:
                        if (allPlayers.Count == 2) UpdatePlayerIndex();
                        Console.WriteLine($"A {Enum.GetName(currentColor)} Reverse card! It's {(GetPlayer().type == Player.Type.YOU ? "your" : GetPlayer().name + "'s")} turn.");
                    break;

                    case Kind.Draw_2:
                        Console.WriteLine($"{currentColor} Draw 2! {GetPlayer().name} {(GetPlayer().type == Player.Type.YOU ? "were" : "was")} forced to draw two cards!");

                        for(int d = 0; d < Math.Clamp(drawPile.Count, 1, 2); d++) AddCard();
                        
                        UpdatePlayerIndex();
                    break;

                    case Kind.Draw_4:
                        Console.WriteLine($"{GetPlayer().name} {(GetPlayer().type == Player.Type.YOU ? "were" : "was")} forced to draw FOUR cards! " + (currentColor != Suit.Unassigned? $"The new color is {Enum.GetName(currentColor)}." :  string.Empty));
                        
                        for(int d = 0; d < Math.Clamp(drawPile.Count, 1, 4); d++) AddCard();

                        UpdatePlayerIndex();
                    break;

                    case Kind.Wild:
                        Console.WriteLine($"A wildcard was played! " + (currentColor != Suit.Unassigned ? $"The new color is {Enum.GetName(currentColor)}." : string.Empty));
                    break;

                    case Kind.Numbered:
                        Console.WriteLine($"A {Enum.GetName(currentColor)} {currentNumber} was played.");
                    break;
                }
            

                // Remove the last card from the last player's deck and move it to the discard pile.

                discardPile.Push(GetDeck(listIndex)[removeIndex]);

                GetDeck(listIndex).RemoveAt(removeIndex);


                await Task.Delay(50);
            }
            
            /// <summary>
            /// Returns what color card the current player has the most of.
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
            
            /// <summary>
            /// The player index variable is incremented or decremented based on the players' order.
            /// </summary>  
            static void UpdatePlayerIndex() {
                playerIndex = NextPlayerIndex();
            }

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
                        Kind.Numbered => $"{Enum.GetName(card.suit)} {card.number}",
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

            // ===============================================================================

            /// <summary>
            /// The logic for your and the CPU's turns. This task will run repeatedly in Main() until someone wins the round.
            /// </summary>
            /// <returns></returns>
            async static Task TakeTurn() {
                bool playCard = false;
                int cardIndex = 0;
                int added = 0;
                Random rnd = new();

                do {
                    await Task.Run(EvaluateTurn);

                    switch(playerIndex) {
                        case 0: // Your turn
                            ViewCards();

                            string input = Console.ReadLine()?.ToUpper();
                   
                            switch(input) {
                                case "ADD":
                                
                                    while(drawPile.Count > 0) {
                                        AddCard();
                                        added++;
                                        if(GetDeck().Any(EvaluateCard) || GetDeck().Any(s => s.suit == Suit.Black)) break;
                                    }

                                    Console.WriteLine($"\n>> You pulled {added} card{(added == 1 ? "." : "s.")}");

                                    added = 0;

                                    await Task.Delay(1500);

                                break;

                                default:
                                    if(int.TryParse(input, out int num)) {
                                        // Evaluate the card that was chosen to see if it can be played.

                                        int index = Math.Clamp(num - 1, 0, GetDeck().Count - 1);

                                        if(GetDeck()[index].suit != Suit.Black) {
                                            if(EvaluateCard(GetDeck()[index])) { 
                                                cardIndex = index;
                                                playCard = true;
                                            }
                                            else { 
                                                Console.WriteLine("Can't play that one; doesn't match either the last card's color or number. "); 
                                                await Task.Delay(1000);

                                                if(!highlight) highlight = true;
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
                                            else currentColor = Suit.Unassigned;
                                            
                                            cardIndex = index;
                                            playCard = true;
                                        }
                                    }
                                    else Console.WriteLine("Invalid input. Try typing 'ADD' for a new card, or the number in parentheses next to a card's name you want to play. ");
                                    
                                break;
                            }
                        break;

                        default: // CPU turn
                            if(GetDeck().Any(EvaluateCard)) {
                                // Picks a random card that either matches the color or number of the last played card.
                                List<int> eval = [];

                                foreach(Card crd in GetDeck().Where(EvaluateCard)) eval.Add(GetDeck().IndexOf(crd));
                                
                                cardIndex = eval[rnd.Next(eval.Count)];

                                // If a black card was picked, then the recommended color is chosen.
                                if(GetDeck()[cardIndex].suit == Suit.Black) currentColor = RecommendColor();

                                playCard = true;
                            }
                            else if(GetDeck().Any(n => n.suit == Suit.Black)) {
                            
                                // Gather all the black cards in the deck.
                                var eval = GetDeck().Where(e => e.suit == Suit.Black);

                                /* 
                                    It's time to choose a new color.
                                    If there are ONLY black cards in the CPU's hand, a random RGBY color is picked.
                                    If their deck consists of black cards AND RGBY cards the CPU can't play, the color the CPU has the most of gets picked.
                                */

                                if(GetDeck().Count - eval.Count() == 0) {
                                    // If their last card is a black card, there's no need to assign a color at all.

                                    if(GetDeck().Count == 1)
                                        currentColor = Suit.Unassigned;
                                    else {
                                        Random rd = new();
                                        int choice = rd.Next(4);
                                        currentColor = (Suit)choice;
                                    }
                                }
                                else currentColor = RecommendColor();

                                /* 
                                    It's time to choose a specific wildcard.
                                    If any of the black cards the CPU has are Draw 4s, and the next player is running low on cards, the CPU will use one. 
                                    It will still use a Draw 4 anyway if there are no wild cards to choose from instead.
                                */                       

                                if(eval.Any(f => f.effect == Kind.Draw_4) && allPlayers.ElementAt(NextPlayerIndex()).Value.Count <= 5) 
                                    cardIndex = GetDeck().IndexOf(GetDeck().First(w=> w.effect == Kind.Draw_4));
                                else if(eval.Any(w => w.effect == Kind.Wild)) 
                                    cardIndex = GetDeck().IndexOf(GetDeck().First(w=> w.effect == Kind.Wild));
                                else 
                                    cardIndex = GetDeck().IndexOf(GetDeck().First(w=> w.effect == Kind.Draw_4));

                                playCard = true;
                            }
                            else {
                                // If there are no wildcards at all and no numbered cards that can be played, the CPU draws a card from the deck.

                                AddCard();
                                added++;
                            }           
                        break;
                    }

                } while(!playCard);

                if(added > 0 && playerIndex != 0) Console.WriteLine($"\n>> {GetPlayer().name} pulled {added} more card{(added == 1 ? "." : "s.")}");

                await PlayCard(GetDeck()[cardIndex]);
                
                if (highlight) highlight = false;
                
                await Task.Delay(2000);
            }

        #endregion

        // =============================================================================

        #endregion

        // =====================================================================================

        async static Task Main() {
            int wins = 0;
            bool noMore = false;

            do {

                #region Init

                // Create players.

                await Task.Run(CreatePlayers);

                // Decide whose going first: you or a CPU.

                string answer = string.Empty;
        
                do {
                    Console.WriteLine("\nAre you dealing? (Y/N)");

                    string choose = Console.ReadLine()?.ToUpper();

                    switch(choose) {
                        case "Y":
                            answer = "!";
                            playerIndex = 1;
                        break;

                        case "N":
                            Random rnd = new();
                            answer = "!";
                            playerIndex = rnd.Next(0, allPlayers.Count);
                            break;

                        default:
                            Console.WriteLine("Just answer the question. (Y/N)");
                            break;
                    }

                } while(answer == string.Empty);

                answer = string.Empty;

                #endregion

                #region Deal

                // The players' hands are filled with 7 cards at the start of the game.

                Console.WriteLine("Distributing cards. Please wait...");

                await Task.Run(CreateCards);

                // Give the players their cards.
                foreach(List<Card> decks in allPlayers.Values)
                    for(int a = 0; a < 7; a++) decks.Add(drawPile.Pop());

                // Start the discard pile with the card at the top of the draw pile.
               
                await Task.Run(RebuildDiscardPile);

                Console.WriteLine("Let's start the game...");

                await Task.Delay(1500);

                #endregion
            
                #region Play

                // The game runs as long as every player still has cards.

                while(!allPlayers.Any(pl => pl.Value.Count == 0)) await Task.Run(TakeTurn);

                var winner = allPlayers.First(pl => pl.Value.Count == 0);
                
                Console.WriteLine($"\n ~~~ GAME OVER! {winner.Key.name} won. ~~~ ");

                if(winner.Key.type == Player.Type.YOU) wins++;

                #endregion

                #region Finish

                // Give the player the option to play again if they want. 

                do {
                    Console.WriteLine("\nDo you want to play again? (Y/N)");

                    string r_answer = Console.ReadLine()?.ToUpper();

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