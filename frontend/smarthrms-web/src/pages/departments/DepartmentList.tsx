import { OrgUnitListView } from '../../components/organization/OrgUnitListView'
import { departmentModule } from '../../components/organization/orgUnits'

export default function DepartmentList() {
  return <OrgUnitListView module={departmentModule} />
}
