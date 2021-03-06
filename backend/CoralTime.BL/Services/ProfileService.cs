﻿using AutoMapper;
using CoralTime.BL.Helpers;
using CoralTime.BL.Interfaces;
using CoralTime.Common.Exceptions;
using CoralTime.Common.Helpers;
using CoralTime.DAL.ConvertersOfViewModels;
using CoralTime.DAL.Models;
using CoralTime.DAL.Repositories;
using CoralTime.ViewModels.DateFormat;
using CoralTime.ViewModels.Member;
using CoralTime.ViewModels.Member.MemberNotificationView;
using CoralTime.ViewModels.Member.MemberPersonalInfoView;
using CoralTime.ViewModels.Member.MemberPreferencesView;
using CoralTime.ViewModels.Profiles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using static CoralTime.Common.Constants.Constants;

namespace CoralTime.BL.Services
{
    public class ProfileService : BaseService, IProfileService
    {
        private readonly IConfiguration _config;
        private readonly IMemberService _memberService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly bool _isDemo;
        private readonly IAvatarService _avatarService;

        public ProfileService(UnitOfWork uow, IConfiguration config, IHttpContextAccessor httpContextAccessor, IMapper mapper, IMemberService memberService, IAvatarService avatarService)
            : base(uow, mapper)
        {
            _config = config;
            _memberService = memberService;
            _httpContextAccessor = httpContextAccessor;
            _isDemo = bool.Parse(_config["DemoSiteMode"]);
            _avatarService = avatarService;
        }

        public DateConvert[] GetDateFormats()
        {
            return DateFormats;
        }

        public List<ProfileProjectView> GetMemberProjects()
        {
            var user = Uow.UserRepository.LinkedCacheGetByName(InpersonatedUserName);
            if (user == null || !user.IsActive)
            {
                throw new CoralTimeEntityNotFoundException($"User with userName {InpersonatedUserName} not found or is not active");
            }

            var member = Uow.MemberRepository.LinkedCacheGetByName(InpersonatedUserName);
            if (member == null)
            {
                throw new CoralTimeEntityNotFoundException($"Member with user with userName {InpersonatedUserName} not found");
            }

            var projects = Uow.MemberProjectRoleRepository.LinkedCacheGetList()
                .Where(r => r.MemberId == member.Id && r.Project.IsActive)
                .Select(r => r.Project)
                .ToList();

            var publicProjects = Uow.ProjectRepository.LinkedCacheGetList()
                .Where(p => !p.IsPrivate && p.IsActive);

            projects.AddRange(publicProjects);

            var allRolesForAllProjects = Uow.MemberProjectRoleRepository.LinkedCacheGetList()
                    .Where(r => projects.Select(p => p.Id).Contains(r.ProjectId) && r.Member.User.IsActive)
                    .ToArray();

            if (projects.Count > 0)
            {
                var managerRoleId = Uow.ProjectRoleRepository.GetManagerRoleId();

                return projects.Select(project => new ProfileProjectView
                {
                    Id = project.Id,
                    ManagersNames = allRolesForAllProjects
                        .Where(x => x.ProjectId == project.Id && x.RoleId == managerRoleId)
                        .Select(x => x.Member.FullName)
                        .ToArray(),
                    MemberCount = allRolesForAllProjects.Count(x => x.ProjectId == project.Id),
                    Name = project.Name,
                    IsPrivate = project.IsPrivate,
                    Color = project.Color,
                    IsPrimary = project.Id == member.DefaultProjectId
                }).ToList();
            }

            return null;
        }

        public List<ProfileProjectMemberView> GetProjectMembers(int projectId)
        {
            var user = Uow.UserRepository.LinkedCacheGetByName(InpersonatedUserName);
            if (user == null || !user.IsActive)
            {
                throw new CoralTimeEntityNotFoundException($"The user with userName {InpersonatedUserName} not found or is not active");
            }

            var member = Uow.MemberRepository.LinkedCacheGetByName(InpersonatedUserName);
            if (member == null)
            {
                throw new CoralTimeEntityNotFoundException($"The member with userName {InpersonatedUserName} not found");
            }

            var project = Uow.ProjectRepository.LinkedCacheGetById(projectId);
            if (project == null || !project.IsActive)
            {
                throw new CoralTimeEntityNotFoundException($"The project with id {projectId} not found or is not active");
            }

            var userHaveAccess = user.IsAdmin
                || !project.IsPrivate
                || Uow.MemberProjectRoleRepository.LinkedCacheGetList().Exists(r => r.ProjectId == projectId && r.MemberId == member.Id);

            if (!userHaveAccess)
            {
                throw new CoralTimeForbiddenException($"User with userName {InpersonatedUserName} has no access to project with id {projectId}");
            }

            IEnumerable<Member> projectMembers;

            if (project.IsPrivate)
            {
                projectMembers = Uow.MemberProjectRoleRepository.LinkedCacheGetList()
                        .Where(r => r.ProjectId == projectId && r.Member.User.IsActive)
                        .Select(r => r.Member);
            }
            else
            {
                projectMembers = Uow.MemberRepository.LinkedCacheGetList()
                        .Where(m => m.User.IsActive);
            }

            var list = projectMembers.Select(m => new ProfileProjectMemberView
            {
                MemberId = m.Id,
                MemberName = m.FullName
            }).ToList();

            foreach (var item in list)
            {
                _avatarService.AddIconUrlInViewModel(item);
            }

            return list;
        }

        public MemberView PatchNotifications(MemberNotificationView memberNotificationView)
        {
            CheckRelatedEntities(InpersonatedUserName, out var memberByName);
            memberByName = Uow.MemberRepository.GetQueryByUserName(InpersonatedUserName);
            memberByName.SendEmailTime = memberNotificationView.SendEmailTime;
            memberByName.SendEmailDays = ConverterBitMask.DayOfWeekStringToInt(memberNotificationView.SendEmailDays);
            memberByName.IsWeeklyTimeEntryUpdatesSend = memberNotificationView.IsWeeklyTimeEntryUpdatesSend;

            try
            {
                Uow.MemberRepository.Update(memberByName);
                Uow.Save();

                Uow.MemberRepository.LinkedCacheClear();

                return memberByName.GetView(Mapper);
            }
            catch (Exception e)
            {
                throw new CoralTimeDangerException("An error occurred while updating member", e);
            }
        }

        public MemberView PatchPreferences(MemberPreferencesView memberPreferencesView)
        {
            CheckRelatedEntities(InpersonatedUserName, out var memberByName);
            memberByName = Uow.MemberRepository.GetQueryByUserName(InpersonatedUserName);
            memberByName.DefaultProjectId = memberPreferencesView.DefaultProjectId;
            memberByName.DefaultTaskId = memberPreferencesView.DefaultTaskId;
            memberByName.TimeZone = memberPreferencesView.TimeZone;
            memberByName.DateFormatId = memberPreferencesView.DateFormatId;
            memberByName.TimeFormat = memberPreferencesView.TimeFormat;
            memberByName.WeekStart = (WeekStart)memberPreferencesView.WeekStart;

            try
            {
                Uow.MemberRepository.Update(memberByName);
                Uow.Save();

                Uow.MemberRepository.LinkedCacheClear();
                return memberByName.GetView(Mapper);
            }
            catch (Exception e)
            {
                throw new CoralTimeDangerException("An error occurred while updating member", e);
            }
        }

        public MemberView PatchPersonalInfo(MemberPersonalInfoView memberPreferencesView)
        {
            if (!EmailChecker.IsValidEmail(memberPreferencesView.Email))
            {
                throw new CoralTimeDangerException("Invalid email");
            }

            if (_isDemo)
            {
                throw new CoralTimeForbiddenException("Full name can't be changed on demo site");
            }

            CheckRelatedEntities(InpersonatedUserName, out var memberByName);
            memberByName = Uow.MemberRepository.GetQueryByUserName(InpersonatedUserName);
            memberByName.FullName = memberPreferencesView.FullName;

            try
            {
                Uow.MemberRepository.Update(memberByName);

                Uow.Save();

                _memberService.ChangeEmailByUserAsync(memberByName, memberPreferencesView.Email).GetAwaiter().GetResult();
                _memberService.UpdateUserClaims(memberByName.Id);

                Uow.MemberRepository.LinkedCacheClear();
                return memberByName.GetView(Mapper);
            }
            catch (Exception e)
            {
                throw new CoralTimeSafeEntityException("An error occurred while updating member", e);
            }
        }

        private void CheckRelatedEntities(string userName, out Member relatedMemberByName)
        {
            GetRelatedUserByName(userName);
            relatedMemberByName = GetRelatedMemberByUserName(userName);
        }

        private ApplicationUser GetRelatedUserByName(string userName)
        {
            var relatedUserByName = Uow.UserRepository.LinkedCacheGetByName(userName);
            if (relatedUserByName == null)
            {
                throw new CoralTimeEntityNotFoundException($"User {userName} not found.");
            }

            if (!relatedUserByName.IsActive)
            {
                throw new CoralTimeEntityNotFoundException($"User {userName} is not active.");
            }

            return relatedUserByName;
        }

        private Member GetRelatedMemberByUserName(string userName)
        {
            var relatedMemberByName = Uow.MemberRepository.LinkedCacheGetByName(userName);
            if (relatedMemberByName == null)
            {
                throw new CoralTimeEntityNotFoundException($"Member with userName {userName} not found.");
            }

            return relatedMemberByName;
        }
    }
}