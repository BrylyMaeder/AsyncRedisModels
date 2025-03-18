using AsyncRedisDocuments;
using AsyncRedisModels.Attributes;
using AsyncRedisModels.Contracts;
using AsyncRedisModels.Index;
using Sample;
using System;
using System.Collections.Generic;
using System.Text;

namespace AsyncRedisModels
{
    [Hydrate]
    public class Character : IAsyncModel
    {
        [Unique]
        public string Username { get; set; }
        public string Id { get; set; }
        public DateTime CreatedAt { get; set; }

        [Hydrate]
        public Character2 myCharacter { get; set; }

        public LinkedModels<Character> Characters => new LinkedModels<Character>(this);
        public LinkedModel<Character> LinkedCharacter => new LinkedModel<Character>(this);
        public LinkedModel<Character> LinkedCharacter2 => new LinkedModel<Character>(this);

        public string IndexName()
        {
            return "character";
        }

        public bool ChangeName() 
        {
            return false;
        }
    }
}
