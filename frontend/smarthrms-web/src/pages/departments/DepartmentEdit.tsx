import { OrgUnitEdit } from '../../components/organization/OrgUnitEditor'
import { departmentModule } from '../../components/organization/orgUnits'

export default function DepartmentEdit() {
  return <OrgUnitEdit module={departmentModule} />
}
