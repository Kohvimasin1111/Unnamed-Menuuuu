using System;
using System.Collections.Generic;
using System.Linq;
using Photon.Realtime;

namespace StupidTemplate.Classes
{
    public class FriendManager
    {
        public const byte FriendByte = 53;

        public static FriendManager instance;

        public FriendsContainer Friends { get; private set; } = new FriendsContainer();

        public FriendManager()
        {
            instance = this;
        }

        public static bool IsPlayerFriend(NetPlayer player)
        {
            if (player == null || instance?.Friends?.friends == null)
                return false;

            return instance.Friends.friends.Values.Any(
                friend => friend.currentUserID == player.UserId
            );
        }

        public class FriendsContainer
        {
            public Dictionary<string, FriendEntry> friends =
                new Dictionary<string, FriendEntry>();
        }

        public class FriendEntry
        {
            public string currentUserID;
        }
    }
}