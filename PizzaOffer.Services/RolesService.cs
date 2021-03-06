﻿using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PizzaOffer.Common;
using PizzaOffer.DataLayer.Context;
using PizzaOffer.DomainClasses;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace PizzaOffer.Services
{
    public interface IRolesService
    {
        Task<Role> FindRoleAsync(string roleName);
        Task<Role> FindRoleAsync(int roleId);
        Task<List<Role>> FindUserRolesAsync(int userId);
        Task<bool> IsUserInRoleAsync(int userId, string roleName);
        Task<List<User>> FindUsersInRoleAsync(string roleName);
        Task AddUserInRoleAsync(int userId, string roleName);
        Task AddUserInRoleAsync(User user, string roleName);
        Task AddUserInRoleAsync(User user, Role role);
        bool IsCurrentUserInRoles(string roleName);
        Task<(bool Succeeded, string Error)> CreateRoleAsync(Role role);
    }

    public class RolesService : IRolesService
    {
        private readonly IUnitOfWork _uow;
        private readonly DbSet<Role> _roles;
        private readonly DbSet<User> _users;
        private readonly DbSet<UserRole> _userRole;
        private readonly IHttpContextAccessor _contextAccessor;

        public RolesService(
            IUnitOfWork uow,
            IHttpContextAccessor contextAccessor)
        {
            _uow = uow;
            _uow.CheckArgumentIsNull(nameof(_uow));

            _contextAccessor = contextAccessor;
            _contextAccessor.CheckArgumentIsNull(nameof(_contextAccessor));

            _roles = _uow.Set<Role>();
            _users = _uow.Set<User>();
            _userRole = _uow.Set<UserRole>();
        }

        public Task<List<Role>> FindUserRolesAsync(int userId)
        {
            var userRolesQuery = from role in _roles
                                 from userRoles in role.UserRoles
                                 where userRoles.UserId == userId
                                 select role;

            return userRolesQuery.OrderBy(x => x.Name).ToListAsync();
        }

        public async Task<bool> IsUserInRoleAsync(int userId, string roleName)
        {
            var userRolesQuery = from role in _roles
                                 where role.Name == roleName
                                 from user in role.UserRoles
                                 where user.UserId == userId
                                 select role;
            var userRole = await userRolesQuery.FirstOrDefaultAsync();
            return userRole != null;
        }

        public Task<List<User>> FindUsersInRoleAsync(string roleName)
        {
            var roleUserIdsQuery = from role in _roles
                                   where role.Name == roleName
                                   from user in role.UserRoles
                                   select user.UserId;
            return _users.Where(user => roleUserIdsQuery.Contains(user.Id))
                         .ToListAsync();
        }

        public async Task AddUserInRoleAsync(int userId, string roleName)
        {
            var role = await _roles.FirstOrDefaultAsync(q => q.Name == roleName);
            var user = await _users.FirstOrDefaultAsync(q => q.Id == userId);
            await AddUserInRoleAsync(user, role);
        }

        public async Task AddUserInRoleAsync(User user, string roleName)
        {
            var role = await _roles.FirstOrDefaultAsync(q => q.Name == roleName);
            await AddUserInRoleAsync(user, role);
        }

        public async Task AddUserInRoleAsync(User user, Role role)
        {
            _userRole.Add(new UserRole { User = user, Role = role });
            await _uow.SaveChangesAsync();
        }

        public bool IsCurrentUserInRoles(string roleName)
        {
            var claimsIdentity = _contextAccessor.HttpContext.User.Identity as ClaimsIdentity;
            var isInRole = claimsIdentity?.FindAll(ClaimTypes.Role).Any(q => q.Value == roleName);
            return isInRole == true;
        }

        public Task<Role> FindRoleAsync(string roleName)
        {
            return _roles.FirstOrDefaultAsync(q => q.Name == roleName);
        }

        public Task<Role> FindRoleAsync(int roleId)
        {
            return _roles.FindAsync(roleId);
        }

        public async Task<(bool Succeeded, string Error)> CreateRoleAsync(Role role)
        {
            if (await _roles.AnyAsync(q => q.Name == role.Name))
            {
                return (false, "This role is already exists.");
            }

            await _roles.AddAsync(role);

            try
            {
                await _uow.SaveChangesAsync();
            }
            catch (System.Exception ex)
            {
                return (false, ex.Message);
            }

            return (true, string.Empty);
        }
    }
}
