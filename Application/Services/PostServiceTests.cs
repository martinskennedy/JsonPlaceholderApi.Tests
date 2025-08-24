using AutoMapper;
using JsonPlaceholderApi.Application.DTOs;
using JsonPlaceholderApi.Application.Services;
using JsonPlaceholderApi.Domain.Entities;
using JsonPlaceholderApi.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Net;
using System.Net.Http.Json;

namespace JsonPlaceholderApi.Tests.Application.Services
{
    public class PostServiceTests
    {
        private readonly Mock<IPostRepository> _postRepositoryMock;
        private readonly IMapper _mapper;

        public PostServiceTests()
        {
            _postRepositoryMock = new Mock<IPostRepository>();

            // Configuração do AutoMapper
            var config = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<PostDto, Post>()
                   .ForMember(dest => dest.Id, opt => opt.Ignore());
                cfg.CreateMap<Post, PostDto>();
            });
            _mapper = config.CreateMapper();
        }

        private HttpClient CreateHttpClientWithPosts(List<PostDto> posts)
        {
            var handler = new FakeHttpMessageHandler(posts);
            return new HttpClient(handler);
        }

        // ==================================
        // TESTES PARA FetchAndSavePostsAsync
        // ==================================

        // Todos os posts já existem
        [Fact]
        public async Task FetchAndSavePostsAsync_AllPostsExist_ReturnsEmptyList()
        {
            var existingPosts = new List<Post>
            {
                new Post { Id = 1, ExternalId = 1, Title = "Post 1", Body = "Body 1" },
                new Post { Id = 2, ExternalId = 2, Title = "Post 2", Body = "Body 2" }
            };

            var apiPosts = new List<PostDto>
            {
                new PostDto { Id = 1, Title = "Post 1", Body = "Body 1" },
                new PostDto { Id = 2, Title = "Post 2", Body = "Body 2" }
            };

            _postRepositoryMock.Setup(r => r.GetAllAsync())
                               .ReturnsAsync(existingPosts);

            var httpClient = CreateHttpClientWithPosts(apiPosts);
            var service = new PostService(_postRepositoryMock.Object, httpClient, _mapper);

            var result = await service.FetchAndSavePostsAsync();

            Assert.Empty(result);
            _postRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Post>()), Times.Never);
        }

        // Falha ao adicionar no repositório
        [Fact]
        public async Task FetchAndSavePostsAsync_ThrowsExceptionOnAdd()
        {
            var apiPosts = new List<PostDto>
            {
                new PostDto { Id = 1, Title = "Post 1", Body = "Body 1" }
            };

            _postRepositoryMock.Setup(r => r.GetAllAsync())
                               .ReturnsAsync(new List<Post>());

            _postRepositoryMock.Setup(r => r.AddAsync(It.IsAny<Post>()))
                               .ThrowsAsync(new DbUpdateException("Erro de banco"));

            var httpClient = CreateHttpClientWithPosts(apiPosts);
            var service = new PostService(_postRepositoryMock.Object, httpClient, _mapper);

            await Assert.ThrowsAsync<DbUpdateException>(() => service.FetchAndSavePostsAsync());
        }

        // ============================
        // TESTES PARA GetAllPostsAsync
        // ============================

        [Fact]
        public async Task GetAllPostsAsync_ReturnsAllPosts()
        {
            var posts = new List<Post>
            {
                new Post { Id = 1, ExternalId = 1, Title = "Post 1", Body = "Body 1" },
                new Post { Id = 2, ExternalId = 2, Title = "Post 2", Body = "Body 2" }
            };

            _postRepositoryMock.Setup(r => r.GetAllAsync())
                               .ReturnsAsync(posts);

            var service = new PostService(_postRepositoryMock.Object, new HttpClient(), _mapper);

            var result = await service.GetAllPostsAsync();

            Assert.Equal(2, result.Count());
        }

        // =================================
        // TESTES PARA GetPostsByUserIdAsync
        // =================================

        [Fact]
        public async Task GetPostsByUserIdAsync_ReturnsFilteredPosts()
        {
            var posts = new List<Post>
            {
                new Post { Id = 1, UserId = 1, ExternalId = 1, Title = "Post 1", Body = "Body 1" },
                new Post { Id = 2, UserId = 2, ExternalId = 2, Title = "Post 2", Body = "Body 2" }
            };

            _postRepositoryMock.Setup(r => r.GetPostsByUserIdAsync(1))
                               .ReturnsAsync(posts.Where(p => p.UserId == 1));

            var service = new PostService(_postRepositoryMock.Object, new HttpClient(), _mapper);

            var result = await service.GetPostsByUserIdAsync(1);

            Assert.Single(result);
            Assert.Equal(1, result.First().UserId);
        }
    }

    // ================================
    // FAKE HTTP HANDLER
    // ================================

    public class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly List<PostDto> _posts;

        public FakeHttpMessageHandler(List<PostDto> posts)
        {
            _posts = posts;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_posts == null)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = null
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(_posts)
            });
        }
    }
}
