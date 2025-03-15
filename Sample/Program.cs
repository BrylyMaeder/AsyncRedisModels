// See https://aka.ms/new-console-template for more information
using AsyncRedisModels;
using AsyncRedisModels.Query;
using AsyncRedisModels.Repository;

RedisSingleton.Initialize("redis-13464.c81.us-east-1-2.ec2.redns.redis-cloud.com", 13464, "4TdQe8UepIdXwrGBGSJwTl5s1nsvYpgN");
Console.WriteLine("Hello, World!");

var character = await RedisRepository.CreateAsync<Character>();

character = await RedisRepository.LoadAsync<Character>(character.Id);
character.Username = "fish@face";

await character.PushAsync(s => s.Username);

await character.PullAsync(s => s.Username);
Console.WriteLine(character.Username);

var query = new RedisQuery<Character>();
var results = await query.Where(s => s.Username == "fish@face").SelectAsync();

Thread.Sleep(-1);