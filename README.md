# Async Redis Models

A better; smarter and faster way to interact with Redis.
While RediJson allows access to json models, direct hash calls remain the best way to maximize performance on Redis. 

Easily perform complex queries using linq and partially load/update your models.


## Setup 

 
**Initialize Redis Connection** 

Start by initializing the Redis connection with your Redis server details:



```csharp
RedisSingleton.Initialize("your-redis-host", port, "your-password");
```

## Basic Usage 


### Create a New Entity 



```csharp
var character = await RedisRepository.CreateAsync<Character>();
```


### Load an Entity from Redis 



```csharp
character = await RedisRepository.LoadAsync<Character>(character.Id);
```


### Update and Persist Data 



```csharp
character.Username = "myUsername";
await character.PushAsync(s => s.Username);
```


### Retrieve Updated Data 



```csharp
await character.PullAsync(s => s.Username);
Console.WriteLine(character.Username);  // Output the updated Username
```


### Query Data 



```csharp
var query = new RedisQuery<Character>();
var results = await query.Where(s => s.Username == "myUsername").SelectAsync();
```


## Model Definition 


### Character Model 



```csharp
public class Character : IAsyncModel
{
    [Indexed(IndexType.Tag)]
    public string Username { get; set; }
    public string Id { get; set; }
    public DateTime CreatedAt { get; set; }

    public string IndexName()
    {
        return "character";
    }
}
```

 
- `Indexed` attribute can be used to index specific fields.
 
- Implements `IAsyncModel` for Redis interaction.


## Key Operations 


### Push Data to Redis 



```csharp
await character.PushAsync(s => s.Username);
```


### Pull Data from Redis 



```csharp
await character.PullAsync(s => s.Username);
```


### Delete an Entity 



```csharp
await character.DeleteAsync();
```


### Increment a Value 



```csharp
await character.IncrementAsync(s => s.myNumber);
```


## Batch Operations 


### Create Multiple Entities 



```csharp
var characters = await RedisRepository.CreateManyAsync<Character>(100);
```


### Load Multiple Entities 



```csharp
var characters = await RedisRepository.LoadManyAsync<Character>(new[] { "id1", "id2" });
```


## Error Handling 


Exceptions are thrown if Redis interactions fail (e.g., if an entity with the same ID already exists).


## Extensions 

 
- **PushAsync** : Save model properties to Redis.
 
- **PullAsync** : Retrieve model properties from Redis.
 
- **IncrementAsync** : Increment numeric properties atomically.


## Notes 


- RedisSingleton handles the Redis connection and is used for all database operations.
 
- Models must implement `IAsyncModel` for seamless integration with Redis.