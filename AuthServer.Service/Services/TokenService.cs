﻿using AuthServer.Core.Configuration;
using AuthServer.Core.Dtos;
using AuthServer.Core.Models;
using AuthServer.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SharedLibrary.Configurations;
using SharedLibrary.Services;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace AuthServer.Service.Services
{
    public class TokenService : ITokenService
    {
        private readonly UserManager<UserApp> _userManager;
        private readonly CustomTokenOption _tokenOption;

        public TokenService(IOptions<CustomTokenOption> options, UserManager<UserApp> userManager)
        {
            _tokenOption = options.Value;
            _userManager = userManager;
        }

        private string CreateRefreshToken()
        {
            var numberByte = new Byte[32];
            using var rnd = RandomNumberGenerator.Create();
            rnd.GetBytes(numberByte);

            return Convert.ToBase64String(numberByte);
        }

        private IEnumerable<Claim> GetClaims(UserApp userApp, List<String> audiences)
        {
            var userClaimList = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier,userApp.Id),
                new Claim(JwtRegisteredClaimNames.Email,userApp.Email),
                new Claim(ClaimTypes.Name,userApp.UserName),
                new Claim(JwtRegisteredClaimNames.Jti,Guid.NewGuid().ToString()) 
            };

            userClaimList.AddRange(audiences.Select(x => new Claim(JwtRegisteredClaimNames.Aud, x))); 

            return userClaimList;
        }

        private IEnumerable<Claim> GetClaimsByClient(Client client)
        {
            var claims = new List<Claim> {
            new Claim(JwtRegisteredClaimNames.Jti,Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Sub,client.Id) 
            };

            claims.AddRange(client.Audiences.Select(x => new Claim(JwtRegisteredClaimNames.Aud, x)));

            return claims;
        }

        public TokenDto CreateToken(UserApp userApp)
        {
            var accessTokenExpiration = DateTime.Now.AddMinutes(_tokenOption.AccessTokenExpiration);
            var refreshTokenExpiration = DateTime.Now.AddMinutes(_tokenOption.RefreshTokenExpiration);
            var securityKey = SignService.GetSymmetricalSecurityKey(_tokenOption.SecurityKey);
           
            SigningCredentials signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256Signature);
           
            JwtSecurityToken jwtSecurityToken = new JwtSecurityToken(
               issuer: _tokenOption.Issuer, 
               expires: accessTokenExpiration, 
               notBefore: DateTime.Now,
               claims: GetClaims(userApp, _tokenOption.Audience),
               signingCredentials: signingCredentials
               );

            var handler = new JwtSecurityTokenHandler();

            var token = handler.WriteToken(jwtSecurityToken);

            var tokenDto = new TokenDto
            {
                AccessToken=token,
                RefreshToken=CreateRefreshToken(),
                AccessTokenExpiration=accessTokenExpiration,
                RefreshTokenExpiration=refreshTokenExpiration
            };

            return tokenDto;
        }

        public ClientTokenDto CreateTokenByClient(Client client)
        {
            var accessTokenExpiration = DateTime.Now.AddMinutes(_tokenOption.AccessTokenExpiration);
            var securityKey = SignService.GetSymmetricalSecurityKey(_tokenOption.SecurityKey);

            SigningCredentials signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256Signature);

            JwtSecurityToken jwtSecurityToken = new JwtSecurityToken(
               issuer: _tokenOption.Issuer, 
               expires: accessTokenExpiration, 
               notBefore: DateTime.Now, 
               claims: GetClaimsByClient(client),
               signingCredentials: signingCredentials
               );

            var handler = new JwtSecurityTokenHandler();

            var token = handler.WriteToken(jwtSecurityToken);

            var tokenDto = new ClientTokenDto
            {
                AccessToken = token,
                AccessTokenExpiration = accessTokenExpiration,
            };

            return tokenDto;
        }
    }
}
