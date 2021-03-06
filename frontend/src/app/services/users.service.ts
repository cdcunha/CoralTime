import { UserProject } from '../models/user-project';
import { Observable } from 'rxjs';
import { Injectable } from '@angular/core';
import { PagedResult, ODataServiceFactory, ODataService } from './odata';
import { User } from '../models/user';
import { Project } from '../models/project';
import { ConstantService } from '../core/constant.service';
import { CustomHttp } from '../core/custom-http';

@Injectable()
export class UsersService {
	readonly odata: ODataService<User>;

	constructor(private constantService: ConstantService,
	            private http: CustomHttp,
	            private odataFactory: ODataServiceFactory) {
		this.odata = this.odataFactory.CreateService<User>('Members');
	}

	assignProjectToUser(userId: number, projectId: number, roleId: number): Observable<any> {
		let odata = this.odataFactory.CreateService<UserProject>('MemberProjectRoles');
		let userProject = new UserProject({
			projectId: projectId,
			roleId: roleId,
			memberId: userId
		});
		return odata.Post(userProject);
	}

	assignUserToProject(projectId: number, userId: number, roleId: number) {
		let odata = this.odataFactory.CreateService<UserProject>('MemberProjectRoles');
		let userProject = new UserProject({
			projectId: projectId,
			roleId: roleId,
			memberId: userId
		});
		return odata.Post(userProject);
	}

	changeRole(userProjectId: number, roleId: number): Observable<any> {
		let odata = this.odataFactory.CreateService<UserProject>('MemberProjectRoles');
		let newRoleId = {
			roleId: roleId
		};
		return odata.Patch(newRoleId, userProjectId.toString());
	}

	getProjectUsersWithCount(event, filterStr = '', projectId: number): Observable<PagedResult<UserProject>> {
		let odata = this.odataFactory.CreateService<UserProject>('MemberProjectRoles');

		let filters = [];
		let query = odata
			.Query()
			.Top(event.rows)
			.Skip(event.first);

		if (event.sortField) {
			query.OrderBy('memberName ' + (event.sortOrder === 1 ? 'asc' : 'desc'));
		} else {
			query.OrderBy('memberName asc');
		}

		if (filterStr) {
			filters.push('contains(tolower(memberName),\'' + filterStr.trim().toLowerCase() + '\')');
		}

		filters.push('projectId eq ' + projectId);
		filters.push('isMemberActive eq true');
		query.Filter(filters.join(' and '));

		return query.ExecWithCount().map(res => {
			res.data = res.data.map((x: any) => new UserProject(x));
			return res;
		});
	}

	getUsersWithCount(event, filterStr = '', isActive?: boolean): Observable<PagedResult<User>> {
		let filters = [];
		let query = this.odata
			.Query()
			.Top(event.rows)
			.Skip(event.first);

		if (event.sortField) {
			query.OrderBy(event.sortField + ' ' + (event.sortOrder === 1 ? 'asc' : 'desc'));
		} else {
			query.OrderBy('fullName' + ' ' + (event.sortOrder === 1 ? 'asc' : 'desc'));
		}

		if (filterStr) {
			filters.push('contains(tolower(userName),\'' +
				filterStr.trim().toLowerCase() + '\')' +
				' or contains(tolower(fullName),\'' +
				filterStr.trim().toLowerCase() + '\')' +
				' or contains(tolower(email),\'' + filterStr.trim().toLowerCase() + '\')');
		}

		if (typeof isActive !== 'undefined') {
			filters.push('isActive eq ' + isActive);
		}

		if (filters.length) {
			query.Filter(filters.join(' and '));
		}

		return query.ExecWithCount().map(res => {
			res.data = res.data.map((x: any) => new User(x));
			return res;
		});
	}

	getUserByEmail(email: string): Observable<User> {
		email = email.trim().toLowerCase();
		if (!email) {
			throw new Error('Please, specify email');
		}

		let query = this.odata
			.Query()
			.Top(1);

		query.Filter('tolower(email) eq \'' + email + '\'');

		return query.Exec()
			.flatMap(result => {
				let user = result[0] ? new User(result[0]) : null;
				return Observable.of(user);
			});
	}

	getUserById(id: number): Observable<User> {
		return this.http.get(this.constantService.apiBaseUrl + '/odata/Members(' + id + ')')
			.map(res => new User(res.json()));
	}

	getUserByUsername(username: string): Observable<User> {
		username = username.trim().toLowerCase();
		if (!username) {
			throw new Error('Please, specify username');
		}

		let query = this.odata
			.Query()
			.Top(1);

		query.Filter('tolower(userName) eq \'' + username + '\'');

		return query.Exec()
			.flatMap(result => {
				let user = result[0] ? new User(result[0]) : null;
				return Observable.of(user);
			});
	}

	getUserProjectsWithCount(event, filterStr = '', memberId: number): Observable<PagedResult<UserProject>> {
		let odata = this.odataFactory.CreateService<UserProject>('MemberProjectRoles');

		let filters = [];
		let query = odata
			.Query()
			.Top(event.rows)
			.Skip(event.first);

		if (event.sortField) {
			query.OrderBy(event.sortField + ' ' + (event.sortOrder === 1 ? 'asc' : 'desc'));
		} else {
			query.OrderBy('projectName asc');
		}

		if (filterStr) {
			filters.push('contains(tolower(projectName),\'' + filterStr.trim().toLowerCase() + '\')');
		}

		filters.push('memberId eq ' + memberId);
		filters.push('isProjectActive eq true');
		query.Filter(filters.join(' and '));

		return query.ExecWithCount().map(res => {
			res.data = res.data.map(x => new UserProject(x));
			return res;
		});
	}

	getUnassignedProjectsWithCount(event, filterStr = '', memberId: number): Observable<PagedResult<Project>> {
		let odata = this.odataFactory.CreateService<Project>('MemberProjectRoles(' + memberId + ')/projects');

		let filters = [];
		let query = odata
			.Query()
			.Top(event.rows)
			.Skip(event.first);

		if (event.sortField) {
			query.OrderBy(event.sortField + ' ' + (event.sortOrder === 1 ? 'asc' : 'desc'));
		} else {
			query.OrderBy('name asc');
		}

		if (filterStr) {
			filters.push('contains(tolower(name),\'' + filterStr.trim().toLowerCase() + '\')');
		}

		filters.push('isActive eq true');
		filters.push('isPrivate eq true');
		query.Filter(filters.join(' and '));

		return query.ExecWithCount().map(res => {
			res.data = res.data.map((x: any) => new Project(x));
			return res;
		});
	}

	getUnassignedUsersWithCount(event, filterStr = '', projectId: number): Observable<PagedResult<User>> {
		let odata = this.odataFactory.CreateService<User>('MemberProjectRoles(' + projectId + ')/members');

		let filters = [];
		let query = odata
			.Query()
			.Top(event.rows)
			.Skip(event.first);

		if (event.sortField) {
			query.OrderBy('fullName ' + (event.sortOrder === 1 ? 'asc' : 'desc'));
		} else {
			query.OrderBy('fullName asc');
		}

		if (filterStr) {
			filters.push('contains(tolower(fullName),\'' + filterStr.trim().toLowerCase() + '\')');
		}

		filters.push('isActive eq true');
		query.Filter(filters.join(' and '));

		return query.ExecWithCount().map(res => {
			res.data = res.data.map((x: any) => new User(x));
			return res;
		});
	}

	removeFromProject(userProject: UserProject): Observable<any> {
		let odata = this.odataFactory.CreateService<UserProject>('MemberProjectRoles');

		return odata.Delete(userProject.id.toString());
	}
}
