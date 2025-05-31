import { Outlet } from "react-router-dom";
import Header from "../component/header/header";
import Footer from "../component/footer/footer";

const MainLayout = () => {
    return (
         <>
       <div className="main-layout-container">
        <div className="main-layout-header">
          <Header />
        </div>
        <div className="main-layout-content">
          <Outlet />
        </div>
        <div className="main-layout-footer">
          <Footer />
        </div>
      </div>
      </>
    )
}

export default MainLayout