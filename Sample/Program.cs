// See https://aka.ms/new-console-template for more information
using AsyncRedisModels;
using AsyncRedisModels.Query;
using AsyncRedisModels.Repository;
using System.Linq.Expressions;

RedisSingleton.Initialize("redis-13464.c81.us-east-1-2.ec2.redns.redis-cloud.com", 13464, "4TdQe8UepIdXwrGBGSJwTl5s1nsvYpgN");
Console.WriteLine("Hello, World!");

var creationResult = await RedisRepository.CreateAsync<Character>();
if (!creationResult.Succeeded)
{
    throw new Exception(creationResult.Message);
}

var character = creationResult.Data;
character = await RedisRepository.LoadAsync<Character>(character.Id);
character.Username = "fish@face.com";

await character.PushAsync(s => s.Username);

var pushResult = await character.PushAsync(s => s.Username);

await character.PullAsync(s => s.Username);
await character.Characters.AddOrUpdateAsync(character);
await character.LinkedCharacter.SetAsync(character);
await character.LinkedCharacter2.SetAsync(character);

Console.WriteLine(character.Username);

var query = RedisRepository.Query<Character>(s => s.Username == "fish@face.com");
Console.WriteLine(query.Conditions[0]);
var results = await query.ToListAsync();
Thread.Sleep(-1);
