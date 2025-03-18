// See https://aka.ms/new-console-template for more information
using AsyncRedisModels;
using AsyncRedisModels.Query;
using AsyncRedisModels.Repository;
using System.Linq.Expressions;

RedisSingleton.Initialize("host", port:0000, "password");
Console.WriteLine("Hello, World!");

var creationResult = await RedisRepository.CreateAsync<Character>("test");
if (!creationResult.Succeeded)
{
    //throw new Exception(creationResult.Message);
}

var character = creationResult.Data;
Console.WriteLine($"Id: {character.Id}");

character = await RedisRepository.LoadAsync<Character>("test");
Console.WriteLine($"Id: {character.Id}");

character.Username = "fish@face.com";

await character.PushAsync(s => s.Username);
character.myNestedObject.IsReal = true;
await character.myNestedObject.PushAsync(s => s.IsReal);
var pushResult = await character.PushAsync(s => s.Username);

await character.PullAsync(s => s.Username);

Console.WriteLine(character.Username);

var query = RedisRepository.Query<Character>(s => s.Username == "fish@face.com");
Console.WriteLine(query.Conditions[0]);
var results = await query.ToListAsync();
Thread.Sleep(-1);
