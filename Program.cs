namespace Uno_Game {

     sealed class UNO_Game {

        #region Cards & Players

        public enum Suit { Red, Blue, Green, Yellow, Black }

        public enum Kind { Numbered, Skip, Reverse, Draw_2, Wild, Draw_4, Swap, Shuffle }

         /// <summary>
        /// All cards are objects with an assigned number, color, point value, and effect.
        /// </summary>
        struct Card(Suit suit, Kind effect, int number, int points) {
            public int number = number;
            public Suit suit = suit;
            public Kind effect = effect;

            public int points = points;
        }

        /// <summary>
        /// Every player has a name, and a marker for whether they are a player or CPU.
        /// </summary>
        /// <param name="name">The name of whoever's playing.</param>
        /// <param name="tp">The marker for whether a player is you or a CPU.</param>
        sealed class Player(string name, Player.Type tp) {
            public string name {get; private set;} = name;
            public Type type {get; private set;} = tp;

            public int score = 0;

            public enum Type { YOU, CPU }
        } 

        #endregion

        // =====================================================================================

        #region Global Variables

        // Keeps track of whose turn it is
        static int playerIndex; 
        static bool reverseOrder = false;
        static bool keepScore = false;


        // The color and number of the last played card.
        static Suit currentColor;
        static int currentNumber;
        static bool highlight = false;


        // Keeps track of the status of the draw pile and all players.
        static readonly Stack<Card> drawPile = []; // where every player pulls new cards from
        static readonly Stack<Card> discardPile = []; // where all cards that were played go
        private static List<Card> builder = []; // temporary list for cards that are being reshuffled
        
        static readonly Dictionary<Player, List<Card>> allPlayers = []; // every player and their cards
        
        #endregion

        // =====================================================================================

        #region All Methods

            #region Game Creation 

            async static Task CreateCards(bool newCards) {
                builder.Clear();

                foreach(Suit suit in Enum.GetValues(typeof(Suit))) {
                    if (suit == Suit.Black) break; // We only need this loop for the first four colors.

                    // Create a wild card and a draw 4 card. Since this foreach loop runs four times, there will be four in total for both kinds.
                    builder.Add(new Card(Suit.Black, Kind.Wild, -5, 50));
                    builder.Add(new Card(Suit.Black, Kind.Draw_4, -4, 50));

                    // A for loop that runs twice. Out of it, we get two skips, reverses, and draw 2s, AND two sets of numbered cards 1 through 9.
                    for(int d = 0; d < 2; d++) {
                        builder.Add(new Card(suit, Kind.Skip, -3, 20));
                        builder.Add(new Card(suit, Kind.Reverse, -2, 20));
                        builder.Add(new Card(suit, Kind.Draw_2, -1, 20));

                        // For loop for numbered cards, 1 through 9.
                        for(int n = 1; n < 10; n++) builder.Add(new Card(suit, Kind.Numbered, n, n));
                    }

                    // Finally, every RGBY suit has one 0 card.
                    builder.Add(new Card(suit, Kind.Numbered, 0, 0));
                }

                if(newCards) {
                    Random r = new();
                    builder.Add(new Card(Suit.Black, r.Next(2) == 0 ? Kind.Swap : Kind.Shuffle, -6, 40));
                }

                await Task.Run(RebuildDrawPile);
            }

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

            async static Task DealCards() {
                // Give the players their cards.
                foreach(List<Card> decks in allPlayers.Values)
                    for(int a = 0; a < 7; a++) decks.Add(drawPile.Pop());

                await Task.Delay(250);
            }

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


                // Describe what just entered the discard pile.

                string desc = $">> { firstCard.suit switch {
                
                    Suit.Black => $"{Enum.GetName(typeof(Kind), firstCard.effect)} card",
                    _ => $"{currentColor} {(firstCard.effect == Kind.Numbered ? currentNumber : Enum.GetName(typeof(Kind), firstCard.effect))}" 
                    }

                } added to the discard pile.";

                Console.WriteLine(desc.Replace('_',' '));


                // If the first card is an action card or a wild card (that isnt a draw 4), it concerns the first player.

                if(firstCard.effect != Kind.Numbered) {
                    switch(firstCard.effect) {
                            case Kind.Reverse:
                                reverseOrder = true;
                            break;

                            case Kind.Draw_2:
                                for(int d = 0; d < 2; d++) AddCard();
                            break;

                            case Kind.Wild or Kind.Swap or Kind.Shuffle:
                                
                                if(firstCard.effect == Kind.Swap) {
                                   int picked;

                                    if(playerIndex == 0) picked = allPlayers.Count > 2 ? SwapChoice() : 1;         
                                    else {
                                        if(allPlayers.Count > 2) {
                                            int[] choices = [.. Enumerable.Range(0, allPlayers.Count).Except([playerIndex])];

                                            picked = choices[Random.Shared.Next(0, choices.Length)];
                                        } else picked = 0;
                                    }

                                    await SwapDecks(playerIndex, picked);

                                    Console.WriteLine($"\n>> { (GetPlayer().type == Player.Type.YOU ? "You" : GetPlayer().name) } swapped decks with { (GetPlayer(picked).type == Player.Type.YOU ? "you" : GetPlayer(picked).name) }.");
                                }
                                
                                // Shuffle cards act like regular wild cards at the start of the game, so no ShuffleCards() here.

                                // Whoever is going first gets to pick the color.

                                if (playerIndex == 0) { 
                                    Console.WriteLine("What color card should this game start with?\n");
                                    ViewCards();

                                    Console.WriteLine("\n");

                                    currentColor = ChooseYourColor();
                                }
                                else currentColor = RecommendColor(playerIndex);               
                            break;
                        }

                    UpdatePlayerIndex();
                }

                Console.Write($"{ (firstCard.suit == Suit.Black ? $"The first color is {currentColor}." : string.Empty) }\n" );

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
            
            static void ResetGame() {
                reverseOrder = false;
                highlight = false;
                drawPile.Clear();
                discardPile.Clear();
                allPlayers.Clear();
            }

            #endregion

            // =============================================================================

            #region Gameplay Flow 
            
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
            /// The task that will run when you play a wildcard. You get to choose an RGBY color.
            /// </summary>
            /// <returns></returns>
            static Suit ChooseYourColor() {
                while(true) {
                    string c_input = Console.ReadLine();

                    if(Enum.TryParse(c_input, out Suit result) && !int.TryParse(c_input, out _)) {
                        if(!result.Equals(Suit.Black)) return result;
                        else Console.WriteLine("That can't be used. Choose Red, Blue, Yellow, or Green (Case-sensitive). ");
                    } else Console.WriteLine("Invalid input. Choose Red, Blue, Yellow, or Green (Case-sensitive). ");
                }
            }

            /// <summary>
            /// Reshuffles the cards from the discard pile and adds them back into the draw pile.
            /// </summary>
            /// <returns></returns>
            async static Task EmptyDiscardPile() {
                while(discardPile.Count > 0) builder.Add(discardPile.Pop());

                await Task.Run(RebuildDrawPile);
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
                if(drawPile.Count == 0) {
                    Console.WriteLine("\n>> The draw pile is empty. Reshuffling...");
                    await Task.Run(EmptyDiscardPile);
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
            /// 
            /// </summary>
            /// <param name="index"></param>
            /// <returns>Gets a player at a specific index of the all players dictionary.</returns>
            static Player GetPlayer(int index) => allPlayers.ElementAt(index).Key;

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
            /// 
            /// </summary>
            /// <param name="afterYou"></param>
            /// <param name="order">True: Clockwise False: Counter-clockwise/Reverse</param>
            /// <returns>The player who is after the one whose index was provided as the first argument</returns>
            static int NextPlayerIndex(int afterYou, bool order) {
                int p = order ? afterYou + 1 : afterYou - 1; 

                if(p >= allPlayers.Count) return 0;
                else if(p < 0) return allPlayers.Count - 1; 

                return p;
            }


            /// <summary>
            /// Perform a different action depending on the card that's being played. 
            /// </summary>
            /// <param name="playThis">The card to be played from the current player's deck</param>
            async static Task PlayCard(Card playThis) {

                // Change the current color (if it's not a black card) and number to the card that was played.

                currentNumber = playThis.number;

                if(!playThis.suit.Equals(Suit.Black)) currentColor = playThis.suit;

                if(playThis.effect == Kind.Reverse) reverseOrder = !reverseOrder;


                // Describe what's happening this turn.

                if(GetDeck().Count == 2) Console.WriteLine($"\n>> {GetPlayer().name} {(GetPlayer().type == Player.Type.YOU ? "have" : "has")} UNO!");

                Console.Write($"\n>> {GetPlayer().name} ({GetDeck().Count - 1})" + $"{(keepScore && GetPlayer().score > 0 ? $"({GetPlayer().score})" : string.Empty)}" + ": ");

                
                bool roundFinished = GetDeck(playerIndex).Count == 1; // True if this card is the current player's last one.
                int ind =  allPlayers.Count == 2 ? (playerIndex == 0 ? 1 : 0) : NextPlayerIndex();

                Console.WriteLine($"{ playThis.effect switch {
                        Kind.Skip => $"A {Enum.GetName(currentColor)} skip card!",
                        Kind.Reverse => $"A {Enum.GetName(currentColor)} Reverse card!",
                        Kind.Draw_2 => $"{currentColor} Draw 2!",
                        Kind.Draw_4 => "It's a Draw 4!",
                        Kind.Wild => "It's a wildcard!",
                        Kind.Swap => "It's a Swap card!",
                        Kind.Shuffle => $"{(GetPlayer().type == Player.Type.YOU ? "You" : GetPlayer().name)} played a Shuffle card!",
                        _=> $"A {Enum.GetName(currentColor)} {currentNumber} was played."}
                        } { (!roundFinished ? playThis.effect switch {
                            Kind.Skip => $"{(GetPlayer(ind).type == Player.Type.YOU ? "Your" : GetPlayer(ind).name + "'s")} turn was skipped.",
                            Kind.Reverse => $"It's {(GetPlayer(ind).type == Player.Type.YOU ? "your" : GetPlayer(ind).name + "'s")} turn.",
                            Kind.Draw_2 => $"{(GetPlayer(ind).type == Player.Type.YOU ? "You were" : GetPlayer(ind).name + " was")} forced to draw two cards!",
                            Kind.Draw_4 => $"{GetPlayer(ind).name} {(GetPlayer(ind).type == Player.Type.YOU ? "were" : "was")} forced to draw four cards!",
                            _=> string.Empty,
                        }  : string.Empty)}");


                // Perform this card's effect on the next player if the current player isn't playing their very last card.

                int prevPlayerIndex = playerIndex;
                int removeIndex = GetDeck().IndexOf(playThis);

                if(!roundFinished) {
                    UpdatePlayerIndex();

                    switch(playThis.effect) {
                        case Kind.Skip:
                            UpdatePlayerIndex();
                        break;

                        case Kind.Reverse:
                            if (allPlayers.Count == 2) UpdatePlayerIndex();
                        break;

                        case Kind.Draw_2:
                            for(int d = 0; d < Math.Clamp(drawPile.Count, 1, 2); d++) AddCard();
                            
                            UpdatePlayerIndex();
                        break;

                        case Kind.Wild or Kind.Draw_4:
                            if(playThis.effect == Kind.Draw_4) for(int d = 0; d < Math.Clamp(drawPile.Count, 1, 4); d++) AddCard();
                            
                            // You or the CPU that played this card can choose a color.
                            switch(prevPlayerIndex) {
                                case 0:
                                    Console.Write("\nWhat color card should the next player put down? ");

                                    currentColor = ChooseYourColor();
                                break;

                                default:
                                    // CPUs will pick a random color if there's only black cards in their deck. Otherwise, they pick what they have the most of.
                                    currentColor = RecommendColor(prevPlayerIndex);
                                break;
                            }

                            Console.WriteLine($"The new color is {Enum.GetName(currentColor)}.");

                            if(playThis.effect == Kind.Draw_4) UpdatePlayerIndex();
                        break;
                    }
                }


                // Remove this card from its deck and move it to the discard pile.

                discardPile.Push(GetDeck(prevPlayerIndex)[removeIndex]);

                GetDeck(prevPlayerIndex).RemoveAt(removeIndex);


                // Swap or Shuffle decks if either of the two cards were played.

                if(!roundFinished && (playThis.effect == Kind.Swap || playThis.effect == Kind.Shuffle)) { 
                    switch(playThis.effect) {
                        case Kind.Shuffle:
                            Console.WriteLine("\n>> Rearranging decks...");

                            await Task.Run(ShuffleDecks);     
                        break;

                        case Kind.Swap:
                            int swapIndex = allPlayers.Count > 2 ? (GetPlayer(prevPlayerIndex).type == Player.Type.YOU ? SwapChoice() : RecommendSwap(prevPlayerIndex)) : playerIndex;

                            Console.WriteLine($"\n>> {GetPlayer(prevPlayerIndex).name} chose to swap decks with {(GetPlayer(swapIndex).type == Player.Type.YOU ? "you" : GetPlayer(swapIndex).name)}.");
                            
                            await SwapDecks(prevPlayerIndex, swapIndex);

                            break;
                        }

                    // Choose a color now.
                    if(prevPlayerIndex == 0) {
                        Console.WriteLine("\nYou've been given a new deck. Choose a new color... ");

                        ViewCards();

                        Console.WriteLine("\n(Red, Blue, Yellow or Green (case-sensitive).) ");

                        currentColor = ChooseYourColor();
                    } else currentColor = RecommendColor(prevPlayerIndex);

                     Console.WriteLine($"The new color is {Enum.GetName(currentColor)}.");
                }

                await Task.Delay(50);
            }
            

            /// <summary>
            /// 
            /// </summary>
            /// <returns>A random RGBY color.</returns>
            static Suit RandomColor() {
                Random rd = new();
                int choice = rd.Next(4);
                return (Suit)choice;
            }

            /// <summary>
            /// The color a CPU is likely to pick after playing a wildcard.
            /// </summary>
            /// <param name="toWho">The index of the CPU choosing a color.</param>
            /// <returns></returns>
            static Suit RecommendColor(int toWho) {
                // If all the CPU has is wildcards, a random color is returned.

                if(GetDeck(toWho).Count - GetDeck(toWho).Count(e => e.suit == Suit.Black) == 0) return RandomColor();

                // Otherwise, the color that appears the most in the CPU's deck is returned instead.

                Dictionary<Suit, int> amounts = new() {
                    {Suit.Red, 0},
                    {Suit.Blue, 0},
                    {Suit.Green, 0},
                    {Suit.Yellow, 0}
                };

                foreach(Card card in GetDeck(toWho).Where(d => !d.suit.Equals(Suit.Black))) amounts[card.suit] += 1;
                                
                return amounts.OrderByDescending(x => x.Value).ToList().First().Key;
            }
            
            /// <summary>
            /// The index of the player who the CPU is likely to swap decks with.
            /// </summary>
            /// <param name="whomst">The CPU whose making the choice to swap</param>
            /// <returns></returns>
            static int RecommendSwap(int whomst) {
                Dictionary<int, int> choices = [];

                foreach(int possible in Enumerable.Range(0, allPlayers.Count).Except([whomst])) choices.Add(possible, allPlayers.ElementAt(possible).Value.Count);

                return choices.OrderBy(x => x.Value).ToList().First().Key;
            }

            /// <summary>
            /// The task for when a Shuffle card is played.
            /// </summary>
            /// <returns></returns>
            async static Task ShuffleDecks() {
                // Gather all the cards from every deck.
                List<Card> swapping = [];

                foreach(var deck in allPlayers.Values) {
                    swapping.AddRange(deck);
                    deck.Clear();
                }

                // Shuffle the cards.

                List<Card> newOrder = [.. swapping.OrderBy(_=> Random.Shared.Next())];

                // While the shuffled list still has cards, add cards one by one to each deck.

                int ind = playerIndex;
                while(newOrder.Count > 0) {
                    GetDeck(ind).Add(newOrder[0]);
                    newOrder.RemoveAt(0);
                    ind = NextPlayerIndex(ind, true);
                }

                await Task.Delay(100);
            }

            /// <summary>
            /// The task that lets you choose the player you want to swap with.
            /// </summary>
            /// <returns></returns>
            static int SwapChoice() {
                Console.WriteLine("Whose deck do you want? ");

                while(true) {
                    string s_input = Console.ReadLine();

                    if(int.TryParse(s_input, out int op)) return Math.Clamp(op - 1, 1, allPlayers.Count - 1);
                    else Console.WriteLine($"Psst! Choose a # between 2 through {allPlayers.Count}!");
                }
            }

            /// <summary>
            /// Swaps the decks of two players.
            /// </summary>
            /// <param name="player">The index of the player making the swap.</param>
            /// <param name="victim">The index of the player that was chosen for the swap.</param>
            /// <returns></returns>
            async static Task SwapDecks(int player, int victim) {
                List<List<Card>> toSwap = [];
                int[] swapThese = [player, victim];
                
                for(int r = 0; r < 2; r++) {
                    toSwap.Add([]);
                    toSwap[r].AddRange(GetDeck(swapThese[r]));
                }

                toSwap.Reverse();

                for(int s = 0; s < 2 ; s++) {
                    GetDeck(swapThese[s]).Clear();
                    GetDeck(swapThese[s]).AddRange(toSwap[s]);
                }

                await Task.Delay(100);
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
                
                foreach(Card card in GetDeck(0)) {

                    hl = highlight && ( card.suit == Suit.Black || EvaluateCard(card) );

                    if (hl) Console.ForegroundColor = ConsoleColor.Green;

                    string display = $" || ({ind}) { card.effect switch {
                        Kind.Numbered => $"{Enum.GetName(card.suit)} {card.number}",
                        Kind.Skip => $"{Enum.GetName(card.suit)} Skip",
                        Kind.Reverse => $"{Enum.GetName(card.suit)} Reverse",
                        Kind.Draw_2 => $"{Enum.GetName(card.suit)} Draw 2",
                        _=> Enum.GetName(typeof(Kind), card.effect)?.Replace('_', ' ')
                    }} || ";

                    Console.Write(display);

                    if (hl) Console.ResetColor();

                    ind++;
                }
            }

            /// <summary>
            /// Input "Y" for true, "N" for false
            /// </summary>
            /// <param name="question"></param>
            /// <returns></returns>
            static bool YesNo(string question) {
                bool answer = false;
                string wl = string.Empty;

                while(wl == string.Empty) {
                    Console.WriteLine(question);

                    string choice = Console.ReadLine()?.ToUpper();

                    switch(choice) {
                        case "Y":
                            answer = true;
                            wl = "!";
                            break;
                        case "N":
                            wl = "!";
                            break;
                        default:
                            Console.WriteLine("Just answer the question.");
                            break;
                    }
                }

                return answer;
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

                            Console.Write($"\n{ (highlight ? "" : "What will you do? ") }");

                            string input = Console.ReadLine()?.ToUpper();
                   
                            switch(input) {
                                case "A":
                                
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

                                        if(EvaluateCard(GetDeck()[index]) || GetDeck()[index].suit == Suit.Black) { 
                                            cardIndex = index;
                                            playCard = true;
                                        }
                                        else { 
                                            Console.WriteLine("Can't play that one; doesn't match either the last card's color or number. "); 
                                            await Task.Delay(1000);

                                            if(!highlight) highlight = true;
                                        }
                                        
                                    }
                                    else Console.WriteLine("Invalid input. Try typing 'A' for a new card, or the number in parentheses next to a card's name you want to play. ");
                                    
                                break;
                            }
                        break;

                        default: // CPU turn
                            if(GetDeck().Any(EvaluateCard)) {
                                // If there are cards the CPU can play, the index of each playable card is added to the list below.
                                
                                List<int> eval = [];

                                foreach(Card crd in GetDeck().Where(EvaluateCard)) eval.Add(GetDeck().IndexOf(crd));
                                
                                /*  Normally, CPUs are more likely to play their numbered cards than their wild cards.
                                    If you're playing a round with the score system, they'll play cards with lower point
                                    values.
                                */
                                
                                cardIndex = keepScore ? eval.OrderBy(s => GetDeck()[s].points).ToArray()[0] : eval.FirstOrDefault(e => GetDeck()[e].effect == Kind.Numbered, eval[0]);

                                playCard = true;
                            }
                            else if(GetDeck().Any(n => n.suit == Suit.Black)) {
                            
                                // Gather all the black cards in the deck.
                                var eval = GetDeck().Where(e => e.suit == Suit.Black);
                               
                                /* 
                                    It's time to choose a specific wildcard.
                                    Draw 4: If the next player after them has 5 or less cards in their deck.
                                    Swap: If there are any other players with a few cards, and they have more cards than them.
                                    Wild: If the other conditions weren't met.
                                    Default: If the CPU didn't have a wildcard, they simply choose the first black card they have.
                                */                       

                                Kind picked = eval switch {
                                    var ev when ev.Any(f => f.effect == Kind.Draw_4) && allPlayers.ElementAt(NextPlayerIndex()).Value.Count <= 5  => Kind.Draw_4,
                                    var ev when ev.Any(s => s.effect == Kind.Swap) && allPlayers.Any(d => d.Value.Count <= 5) && GetDeck().Count - 1 > GetDeck(RecommendSwap(playerIndex)).Count  => Kind.Swap,
                                    var ev when ev.Any(w => w.effect == Kind.Wild)  => Kind.Wild,
                                    _=> eval.First().effect
                                };

                                cardIndex = GetDeck().IndexOf(eval.First(p => p.effect == picked));

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

        #endregion

        // =====================================================================================

        async static Task Main() {
            int wins = 0;
            bool noMore = false;

            do {

                #region Init

                // Create players.

                await Task.Run(CreatePlayers);

                // Create rules for the game.
        
                playerIndex = YesNo("\nGoing first? (Y/N)") ? 0 : (allPlayers.Count == 2 ? 1 : Random.Shared.Next(1, allPlayers.Count));
                bool newCards = YesNo("\nInclude the Swap/Shuffle Hands card? (Y/N)");
                keepScore = YesNo("\nUse the score system? (Y/N)");

                #endregion

                #region Deal

                // The players' hands are filled with 7 cards at the start of the game.

                await CreateCards(newCards);

                Console.WriteLine("Distributing cards. Please wait...");

                await Task.Run(DealCards);

                await Task.Run(RebuildDiscardPile);


                Console.WriteLine("Let's start the game...");

                await Task.Delay(1500);

                #endregion
            
                #region Play
                
                int roundWinner;

                // The game runs as long as every player still has cards.
                while(true) { 
                    await Task.Run(TakeTurn); 

                    // A winner is found when the player who just took their turn has 0 cards left.
                    if(allPlayers.Values.Any(d => d.Count == 0)) { 

                       roundWinner = allPlayers.ToList().FindIndex(e => e.Equals(allPlayers.First(w => w.Value.Count == 0)));

                        /* If we aren't using the score system the game ends immediately. 
                            Otherwise, we have to check first if the player made over 500 pts yet
                            from the total value of everyone's cards. If they did not, the draw pile
                            is reshuffled and new cards are distributed.
                        */

                       if(keepScore) {
                            builder.Clear();
                            int pts = 0;
                            foreach(var pl in allPlayers) {
                                for(int t = 0; t < pl.Value.Count; t++) { 
                                    pts += pl.Value[t].points;
                                    builder.Add(pl.Value[t]);
                                }

                                pl.Value.Clear();
                            }

                            var possible = GetPlayer(roundWinner);
                            possible.score += pts;
                            Console.WriteLine($"\nFrom everyone's cards, {possible.name} made {pts} points, for a total of {possible.score}.");

                            await Task.Delay(2000);

                            // If someone got 500 points or more, the game can end!
                            if(possible.score >= 500) break;
                            else {
                                // Otherwise, the game starts over.

                                Console.WriteLine("\nRedistributing cards...");

                                while(drawPile.Count > 0) builder.Add(drawPile.Pop());
                                while(discardPile.Count > 0) builder.Add(discardPile.Pop());

                                await Task.Run(RebuildDrawPile);

                                await Task.Run(DealCards);

                                await Task.Run(RebuildDiscardPile);
                            }

                        } else break;
                    }
                }
                

                var winner = GetPlayer(roundWinner);
                
                Console.WriteLine($"\n ~~~ GAME OVER! {winner.name} won. ~~~ ");

                if(winner.type == Player.Type.YOU) wins++;

                #endregion

                #region Finish

                // Give the player the option to play again if they want. 

                if(YesNo("\nDo you want to play again? (Y/N)")) {
                    Console.WriteLine("Starting another round.");
                    ResetGame();
                }
                else noMore = true;

                #endregion

            } while(!noMore);

            Console.WriteLine($"\n ~~~ Game complete! You won {wins} {(wins == 1? "time" : "times")}. ~~~");
        }
    } 
}