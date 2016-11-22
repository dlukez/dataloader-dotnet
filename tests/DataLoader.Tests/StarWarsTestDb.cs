using System.Collections.Generic;

namespace DataLoader.Tests
{
    public class StarWarsTestDb
    {

    }

    public class Human
    {
        public int HumanId { get; set; }
        public string Name { get; set; }
        public string HomePlanet { get; set; }
        public List<Friendship> Friendships { get; set; }

        public override string ToString()
        {
            return HumanId.ToString();
        }
    }

    public class Friendship
    {
        public int FriendshipId { get; set; }
        public int HumanId { get; set; }
        public int DroidId { get; set; }
        public Human Human { get; set; }
        public Droid Droid { get; set; }
    }

    public class Droid
    {
        public int DroidId { get; set; }
        public string Name { get; set; }
        public string PrimaryFunction { get; set; }
        public List<Friendship> Friendships { get; set; }

        public override string ToString()
        {
            return DroidId.ToString();
        }
    }
}