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
                cfg.CreateMap<Post, PostDtoTable>().ReverseMap();
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
            // Arrange
            var posts = new List<Post>
            {
                new Post { Id = 1, ExternalId = 1, UserId = 10, Title = "Post 1", Body = "Body 1" },
                new Post { Id = 2, ExternalId = 2, UserId = 20, Title = "Post 2", Body = "Body 2" }
            };

            _postRepositoryMock.Setup(r => r.GetAllAsync())
                               .ReturnsAsync(posts);

            var service = new PostService(_postRepositoryMock.Object, new HttpClient(), _mapper);

            // Act
            var result = await service.GetAllPostsAsync();

            // Assert
            Assert.NotNull(result);
            var resultList = result.ToList();
            Assert.Equal(2, resultList.Count);

            Assert.Equal(1, resultList[0].Id);
            Assert.Equal(10, resultList[0].UserId);
            Assert.Equal("Post 1", resultList[0].Title);

            Assert.Equal(2, resultList[1].Id);
            Assert.Equal(20, resultList[1].UserId);
            Assert.Equal("Post 2", resultList[1].Title);
        }

        // =================================
        // TESTES PARA GetPostsByUserIdAsync
        // =================================

        [Fact]
        public async Task GetPostsByUserIdAsync_ReturnsFilteredPosts()
        {
            // Arrange
            var posts = new List<Post>
            {
                new Post { Id = 1, UserId = 1, ExternalId = 1, Title = "Post 1", Body = "Body 1" },
                new Post { Id = 2, UserId = 2, ExternalId = 2, Title = "Post 2", Body = "Body 2" }
            };

            _postRepositoryMock.Setup(r => r.GetPostsByUserIdAsync(1))
                               .ReturnsAsync(posts.Where(p => p.UserId == 1));

            var service = new PostService(_postRepositoryMock.Object, new HttpClient(), _mapper);

            // Act
            var result = await service.GetPostsByUserIdAsync(1);

            // Assert
            Assert.Single(result);
            var post = result.First();
            Assert.Equal(1, post.UserId);
            Assert.Equal("Post 1", post.Title);
            Assert.Equal("Body 1", post.Body);
        }

        // =======================
        // TESTES PARA UpdateAsync
        // =======================

        [Fact]
        public async Task UpdateAsync_ShouldReturnUpdatedPost_WhenPostExists()
        {
            // Arrange
            var existingPost = new Post { Id = 1, ExternalId = 101, Title = "Old Title", Body = "Old Body", UserId = 1 };
            var updatedDto = new PostDtoTable { Id = 1, ExternalId = 101, Title = "New Title", Body = "New Body", UserId = 1 };

            _postRepositoryMock.Setup(r => r.GetByIdAsync(1))
                               .ReturnsAsync(existingPost);

            _postRepositoryMock.Setup(r => r.UpdateAsync(existingPost))
                               .Returns(Task.CompletedTask);

            var service = new PostService(_postRepositoryMock.Object, new HttpClient(), _mapper);

            // Act
            var result = await service.UpdateAsync(1, updatedDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("New Title", result.Title);
            Assert.Equal("New Body", result.Body);
            Assert.Equal(101, result.ExternalId);
            _postRepositoryMock.Verify(r => r.UpdateAsync(existingPost), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_ShouldReturnNull_WhenPostDoesNotExist()
        {
            // Arrange
            _postRepositoryMock.Setup(r => r.GetByIdAsync(99))
                               .ReturnsAsync((Post?)null);

            var updatedDto = new PostDtoTable { Id = 99, ExternalId = 200, Title = "Any Title", Body = "Any Body", UserId = 1 };

            var service = new PostService(_postRepositoryMock.Object, new HttpClient(), _mapper);

            // Act
            var result = await service.UpdateAsync(99, updatedDto);

            // Assert
            Assert.Null(result);
            _postRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Post>()), Times.Never);
        }

        // =======================
        // TESTES PARA DeleteAsync
        // =======================

        [Fact]
        public async Task DeleteAsync_ShouldReturnTrue_WhenPostExists()
        {
            // Arrange
            var existingPost = new Post { Id = 1, ExternalId = 101, Title = "To delete", Body = "Body", UserId = 1 };

            _postRepositoryMock.Setup(r => r.GetByIdAsync(1))
                               .ReturnsAsync(existingPost);

            _postRepositoryMock.Setup(r => r.DeleteAsync(existingPost.Id))
                               .Returns(Task.CompletedTask);

            var service = new PostService(_postRepositoryMock.Object, new HttpClient(), _mapper);

            // Act
            var result = await service.DeleteAsync(1);

            // Assert
            Assert.True(result);
            _postRepositoryMock.Verify(r => r.DeleteAsync(existingPost.Id), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_ShouldReturnFalse_WhenPostDoesNotExist()
        {
            // Arrange
            _postRepositoryMock.Setup(r => r.GetByIdAsync(99))
                               .ReturnsAsync((Post?)null);

            var service = new PostService(_postRepositoryMock.Object, new HttpClient(), _mapper);

            // Act
            var result = await service.DeleteAsync(99);

            // Assert
            Assert.False(result);
            _postRepositoryMock.Verify(r => r.DeleteAsync(It.IsAny<int>()), Times.Never);
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
